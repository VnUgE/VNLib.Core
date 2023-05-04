# Key-Value Index Spec
Throughout the essentials library, data structures like ISession must also implement a Utils's interface called `IIndexable<K,V>` , which requires the type to implement an indexer semantic. ISession and IUser require K and V to be of type `System.String` to allow for arbitrary string key and values to be stored in a session and a user instance. This allows for libraries to interact without the requirement for strong typing in shared libraries. However this requires information to be stored as strings, which may be an inefficient data-type, but it allows for much simpler consumption and implementation. So your binary data will need to be encoded at the call site. This also means string keys may collide and you will be required to know what data is stored at a given key across ALL the plugins/libraries your application uses. This spec exists to define my usage and best practices. 

## Name spacing
I chose to integrate key "namespaces" as a way to avoid key (dictionary entry) collision. *Keys* are in lowercase utf-8 as most back-ends will likely use JSON or similar data serialization methods to store data, so you may want to avoid creating encoding overhead by using extended characters. 
Example:
`acnt.ila` - This represents the *account* namespace and the key *ila* which currently stands for  'is-local-account' and the value is an encoded string of a Boolean value. 

### Best practice
I have found that the best way to share index keys across your application is by defining string constants to define the usage, or create an extension library that extends the `IIndexable<K,V>` interface or more specific types for sessions or user types. You you may create an extension class that looks like the following. 

```C#
static class AccountExtensions{
	public const string IsLocalAccountKey = "acnt.ila";
	public bool IsLocalAccount(this IUser user){
		return user[IsLocalAccountKey] == bool.TrueString;
	}
	public void IsLocalAccount(this IUser user, bool value){
		user[IsLocalAccountKey] = value ? bool.TrueString : bool.FalseString;
	}
}
```

This allows you to easily distribute extensible functionality for using stored information within indexable types and avoid key collision. You will notice I did not add a get or set prefix to the above methods. If you start creating features that extend an indexable type, you may spend a lot of time scrolling through your Intellisense menu, which may get annoying, so I choose overloading. It also makes the extension method feel more like property.     

### Forbidden prefixes
For internal library keys, like the internal session extension libraries, keys may not be prefixed with a text namespace, but instead with underscores `__` depending on their level of internal 'depth' so to speak. The underscore is considered a forbidden character for extending indexable types except for in the implementation type or a library that extends the implementation or its internal properties. 

Example:
```C#
	class Impl : IIndexable<string, string>
	{
		const string TimeKey = "__.time";
		IDictionary<string, string> storage;
		string this[string key]{
			get => storage[key];
			set => storage[key] = value;
		}
		//Time property, uses the __ index prefix
		int Time {
			get => int.Parse(this[TimeKey]);
			set => this[TimeKey] = value.ToString();
		}
	}
```

A time property was added to the implementation and an internal key is defined as a constant string with the `__` (double underscore) prefix that defines itself as an internal storage key. This allows the property value to be stored by the implementation in its backing storage. You will find that Essentials library implementations, such as the Sessions and Users libraries use a common string storage with underscore prefix properties to allow for simple backing storage like a database. 

You can see why these underscore prefixed keys are considered forbidden as they are expected to be reserved by the implementation. Direct user access to a storage index with an underscored prefix may corrupt the internal state, so don't do it unless you are extending the implementation. Know that you are doing something funky with the internal storage. **Make sure you know what you are doing!**

## Encoding binary data
As stated above is common for Essentials data structures to implement a string key-value indexer interface. It is also common to store arbitrary binary data in these storage structures, such as secret keys or dates, or other struct types. This is due to the backend storage by the implementation. The simplest and arguably safest *High Level* arbitrary data representation is the String type in C#. Most of these structures will need to serialize and deserialize their storage or state, to or from arbitrary binary storage, back to a high level data structure for consumption. Key-Value storage is usually the go-to and is often very simple to serialize state information. You will find custom serialization in some libraries like the Essentials.Sessions library for performance reasons, and JSON for less performance critical use-cases. 

Keep in mind that C# strings are unicode-UTF-16 encoded values and that is not usually a safe/universal character encoding so its likely that the storage backend will encode the string data to a more safe/supported encoding type such as UTF-8 like JSON. This means characters that are **not** a subset of the UTF-8 (or sometimes JSON) character encoding will incur encoding overhead and consume significantly more UTF-8 characters to encode the extended character. 

You may want to consider choosing an encoding type based on the data type. I commonly use base64 (or base64Url) or even base32 encoding depending on how large the datatype is. This is because some base64 characters are not legal JSON, and incur said overhead, hence, consuming more data in the backing storage class. Base32 stores less information per-byte, but is a proper subset of JSON and will **never** incur JSON encoding overhead. Integer types are often best stored in their base10 or base16 format for easier structure encoding.    

If a database is used the overhead of the database interaction is usually expected to be multiple orders of magnitude slower than the JSON serialization method. 

## Other Notes
You may notices most of the discussed data structures in the Essentials libraries also implement directly or via extensions, storing raw objects in the Key-Value storage. This is usually done by JSON serialization, which means nested JSON strings. This may be desirable for storing complex data types (I often do), but will often add encoding overhead in the backing storage class, so just beware of nesting JSON. 