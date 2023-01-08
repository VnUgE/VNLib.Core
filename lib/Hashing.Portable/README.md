
# VNLib.Hashing.Portable

This library is a collection of common cryptographic functions, optimized using the VNLib.Utils
library for interop and memory management.

## Argon2
This library contains an native library interface with the Argon2 Cryptographic Hashing library. If you wish to use the Argon2 hashing functions, you must include the [Argon2 native library](https://github.com/P-H-C/phc-winner-argon2) in your project, and accept the license.

The Argon2 native libary is lazy loaded and therefor not required for the other functions in this library, if it is not included. You may specify the exact path to the native library by setting the `ARGON2_DLL_PATH`environment variable to the value of the path.

**Notice:**
This library does not, modify, contribute, or affect the functionality of the Argon2 library in any way. 

#### Usage:
```
//Using the managed hash version, inputs may be binary or utf8 chars
string encodedHash = VnArgon2.Hash2id(<password>,<salt>,<secret>,...<argon params>)

//The 'raw' or 'passthru' 2id managed hashing method, binary only
VnArgon2.Hash2id(<passbytes>,<saltbytes><secretbytes>,<rawHashOutput>,...<params>) 

//Verification used CryptographicOperations.FixedTimeEquals for comparison
//managed verification, only valid with previously hashed methods
bool valid = VnArgon2.Verify2id(<rawPass>,<hash>,<encodedHash>)

//Binary only 'raw' or 'passthru' 2id managed verification
bool valid = VnArgon2.Verify2id(<rawPass>,<salt>,<secret>,<rawHashBytes>)
```

## Other Classes

The ManagedHash and RandomHash classes are simple "shortcut" methods for common hashing operations with common data encoding/decoding.

The IdentityUtility namespace includes classes and methods for generating and validating JWE types, such as JWT (Json Web Token) and JWK (Json Web Key), and their various extension/helper methods.

### Basic Usage
```
//RandomHash
byte[] cngBytes = RandomHash.GetRandomBytes();
RandomHash.GetRandomBytes(<binary span>);
string base64 = RandomHash.GetRandomBase64(<size>);
string base32 = RandomHash.GetRandomBase32(<size>);
string hex = RandomHash.GetRandomHex(<size>);
string encodedHash = RandomHash.GetRandomHash(<hashAlg>,<size>,<encoding>);
GUID cngGuid = RandomHash.GetSecureGuid();

//Managed hash
ERRNO result = ManagedHash.ComputeHash(<data>,<args>);
string encoded = ManagedHash.ComputeHash(<data>,<args>);
byte[] rawHash = ManagedHash.ComputeHash(<data>,<args>);

//HMAC
ERRNO result = ManagedHash.ComputeHmac(<key>,<data>,<args>);
string encoded = ManagedHash.ComputeHmac(<key>,<data>,<args>);
byte[] rawHash = ManagedHash.ComputeHmac(<key>,<data>,<args>);


//Parse jwt
using JsonWebToken jwt = JsonWebToken.Parse(<jwtEncodedString>);
bool valid = jwt.verify(<Algorithm>,<hashMethod>...);
//Get the payload (or header, they use the same methods)
T payload = jwt.GetPaylod<T>();//OR
JsonDocument payload = jwt.GetPayload();

//Create new JWT
using JsonWebToken jwt = new(<optionalHeap>);
jwt.WriteHeader(<object or binary>); //Set header

jwt.WritePayload(<object or binary>); //Set by serializing it, or binary

//OR init fluent payload builder
jwt.InitPayloadClaim()
   .AddClaim(<string name>, <object value>)
   ...
   .CommitClaims(); //Serializes the claims and writes them to the JWT payload

jwt.Sign(<HashAlgorithm, RSA, ECDsa>... <params>); //Sign the JWT

string jwtData = jwt.Compile(); //Serialize the JWT
```

### License

The software in this repository is licensed under the GNU GPL version 2.0 (or any later version). 
See the LICENSE files for more information.