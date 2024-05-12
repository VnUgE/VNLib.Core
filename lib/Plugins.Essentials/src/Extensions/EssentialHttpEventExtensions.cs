/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials
* File: EssentialHttpEventExtensions.cs 
*
* EssentialHttpEventExtensions.cs is part of VNLib.Plugins.Essentials which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Essentials is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Plugins.Essentials is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

using VNLib.Net.Http;
using VNLib.Hashing;
using VNLib.Utils;
using VNLib.Utils.Memory.Caching;
using static VNLib.Plugins.Essentials.Statics;

namespace VNLib.Plugins.Essentials.Extensions
{

    /// <summary>
    /// Provides extension methods for manipulating <see cref="HttpEvent"/>s
    /// </summary>
    public static class EssentialHttpEventExtensions
    {      
        

        /*
         * Pooled/tlocal serializers
         */
        private static ThreadLocal<Utf8JsonWriter> LocalSerializer { get; } = new(() => new(Stream.Null));
        private static IObjectRental<JsonResponse> ResponsePool { get; } = ObjectRental.Create(ResponseCtor);
        private static JsonResponse ResponseCtor() => new(ResponsePool);

        #region Response Configuring

        /// <summary>
        /// Attempts to serialize the JSON object (with default SR_OPTIONS) to binary and configure the response for a JSON message body
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="ev"></param>
        /// <param name="code">The <see cref="HttpStatusCode"/> result of the connection</param>
        /// <param name="response">The JSON object to serialzie and send as response body</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="NotSupportedException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="ContentTypeUnacceptableException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CloseResponseJson<T>(this IHttpEvent ev, HttpStatusCode code, T response) => CloseResponseJson(ev, code, response, SR_OPTIONS);

        /// <summary>
        /// Attempts to serialize the JSON object to binary and configure the response for a JSON message body
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="ev"></param>
        /// <param name="code">The <see cref="HttpStatusCode"/> result of the connection</param>
        /// <param name="response">The JSON object to serialzie and send as response body</param>
        /// <param name="options"><see cref="JsonSerializerOptions"/> to use during serialization</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="NotSupportedException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="ContentTypeUnacceptableException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CloseResponseJson<T>(this IHttpEvent ev, HttpStatusCode code, T response, JsonSerializerOptions? options)
        {
            JsonResponse rbuf = ResponsePool.Rent();
            try
            {
                //Serialze the object on the thread local serializer
                LocalSerializer.Value!.Serialize(rbuf, response, options);

                //Set the response as the buffer, 
                ev.CloseResponse(code, ContentType.Json, rbuf);
            }
            catch
            {
                //Return back to pool on error
                ResponsePool.Return(rbuf);
                throw;
            }
        }

        /// <summary>
        /// Attempts to serialize the JSON object to binary and configure the response for a JSON message body
        /// </summary>
        /// <param name="ev"></param>
        /// <param name="code">The <see cref="HttpStatusCode"/> result of the connection</param>
        /// <param name="response">The JSON object to serialzie and send as response body</param>
        /// <param name="type">The type to use during de-serialization</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="NotSupportedException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="ContentTypeUnacceptableException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CloseResponseJson(this IHttpEvent ev, HttpStatusCode code, object response, Type type) => CloseResponseJson(ev, code, response, type, SR_OPTIONS);
        
        /// <summary>
        /// Attempts to serialize the JSON object to binary and configure the response for a JSON message body
        /// </summary>
        /// <param name="ev"></param>
        /// <param name="code">The <see cref="HttpStatusCode"/> result of the connection</param>
        /// <param name="response">The JSON object to serialzie and send as response body</param>
        /// <param name="type">The type to use during de-serialization</param>
        /// <param name="options"><see cref="JsonSerializerOptions"/> to use during serialization</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="NotSupportedException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="ContentTypeUnacceptableException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CloseResponseJson(this IHttpEvent ev, HttpStatusCode code, object response, Type type, JsonSerializerOptions? options)
        {
            JsonResponse rbuf = ResponsePool.Rent();
            try
            {
                //Serialze the object on the thread local serializer
                LocalSerializer.Value!.Serialize(rbuf, response, type, options);

                //Set the response as the buffer, 
                ev.CloseResponse(code, ContentType.Json, rbuf);
            }
            catch
            {
                //Return back to pool on error
                ResponsePool.Return(rbuf);
                throw;
            }
        }

        /// <summary>
        /// Writes the <see cref="JsonDocument"/> data to a temporary buffer and sets it as the response
        /// </summary>
        /// <param name="ev"></param>
        /// <param name="code">The <see cref="HttpStatusCode"/> result of the connection</param>
        /// <param name="data">The <see cref="JsonDocument"/> data to send to client</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="ContentTypeUnacceptableException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CloseResponseJson(this IHttpEvent ev, HttpStatusCode code, JsonDocument data)
        {
            ArgumentNullException.ThrowIfNull(ev);

            if(data == null)
            {
                ev.CloseResponse(code);
                return;
            }

            JsonResponse rbuf = ResponsePool.Rent();
            try
            {
                //Serialze the object on the thread local serializer
                LocalSerializer.Value!.Serialize(rbuf, data);

                //Set the response as the buffer, 
                ev.CloseResponse(code, ContentType.Json, rbuf);
            }
            catch
            {
                //Return back to pool on error
                ResponsePool.Return(rbuf);
                throw;
            }
        }

        /// <summary>
        /// Close as response to a client with an <see cref="HttpStatusCode.OK"/> and serializes a <see cref="WebMessage"/> as the message response
        /// </summary>
        /// <param name="ev"></param>
        /// <param name="webm">The <see cref="WebMessage"/> to serialize and response to client with</param>
        /// <exception cref="ContentTypeUnacceptableException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CloseResponse<T>(this IHttpEvent ev, T webm) where T:WebMessage
        {
            ArgumentNullException.ThrowIfNull(ev);

            if (webm is null)
            {
                ev.CloseResponse(HttpStatusCode.OK);
            }
            else
            {
                //Respond with json data
                ev.CloseResponseJson(HttpStatusCode.OK, webm);
            }
        }

        /// <summary>
        /// Close a response to a connection with a file as an attachment (set content dispostion)
        /// </summary>
        /// <param name="ev"></param>
        /// <param name="code">Status code</param>
        /// <param name="file">The <see cref="FileInfo"/> of the desired file to attach</param>
        /// <exception cref="IOException"></exception>
        /// <exception cref="FileNotFoundException"></exception>
        /// <exception cref="UnauthorizedAccessException"></exception>
        /// <exception cref="ContentTypeUnacceptableException"></exception>
        /// <exception cref="System.Security.SecurityException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CloseResponseAttachment(this IHttpEvent ev, HttpStatusCode code, FileInfo file)
        {
            ArgumentNullException.ThrowIfNull(ev);
            ArgumentNullException.ThrowIfNull(file);

            //Close with file
            ev.CloseResponse(code, file);
            //Set content dispostion as attachment (only if successfull)
            ev.Server.Headers["Content-Disposition"] = $"attachment; filename=\"{file.Name}\"";
        }
        
        /// <summary>
        /// Close a response to a connection with a file as an attachment (set content dispostion)
        /// </summary>
        /// <param name="ev"></param>
        /// <param name="code">Status code</param>
        /// <param name="file">The <see cref="FileStream"/> of the desired file to attach</param>
        /// <exception cref="ContentTypeUnacceptableException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CloseResponseAttachment(this IHttpEvent ev, HttpStatusCode code, FileStream file)
        {
            ArgumentNullException.ThrowIfNull(ev);
            ArgumentNullException.ThrowIfNull(file);

            //Close with file
            ev.CloseResponse(code, file);
            //Set content dispostion as attachment (only if successfull)
            ev.Server.Headers["Content-Disposition"] = $"attachment; filename=\"{file.Name}\"";
        }
       
        /// <summary>
        /// Close a response to a connection with a file as an attachment (set content dispostion)
        /// </summary>
        /// <param name="ev"></param>
        /// <param name="code">Status code</param>
        /// <param name="data">The data to straem to the client as an attatcment</param>
        /// <param name="ct">The <see cref="ContentType"/> that represents the file</param>
        /// <param name="fileName">The name of the file to attach</param>
        /// <param name="length">Explicit length of the stream data</param>
        /// <exception cref="ContentTypeUnacceptableException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CloseResponseAttachment(this IHttpEvent ev, HttpStatusCode code, ContentType ct, Stream data, string fileName, long length)
        {
            ArgumentNullException.ThrowIfNull(ev);
            ArgumentNullException.ThrowIfNull(data);

            //Close with file
            ev.CloseResponse(code, ct, data, length);
            //Set content dispostion as attachment (only if successfull)
            ev.Server.Headers["Content-Disposition"] = $"attachment; filename=\"{fileName}\"";
        }

        /// <summary>
        /// Close a response to a connection with a file as the entire response body (not attachment)
        /// </summary>
        /// <param name="ev"></param>
        /// <param name="code">Status code</param>
        /// <param name="file">The <see cref="FileInfo"/> of the desired file to attach</param>
        /// <exception cref="IOException"></exception>
        /// <exception cref="FileNotFoundException"></exception>
        /// <exception cref="UnauthorizedAccessException"></exception>
        /// <exception cref="ContentTypeUnacceptableException"></exception>
        /// <exception cref="System.Security.SecurityException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CloseResponse(this IHttpEvent ev, HttpStatusCode code, FileInfo file)
        {
            ArgumentNullException.ThrowIfNull(ev);
            ArgumentNullException.ThrowIfNull(file);

            //Open filestream for file
            FileStream fs = file.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
            try
            {
                //Set the input as a stream
                ev.CloseResponse(code, fs);
                //Set last modified time only if successfull
                ev.Server.Headers[HttpResponseHeader.LastModified] = file.LastWriteTimeUtc.ToString("R");
            }
            catch 
            {
                //If their is an exception close the stream and re-throw
                fs.Dispose();
                throw;
            }
        }
        
        /// <summary>
        /// Close a response to a connection with a <see cref="FileStream"/>  as the entire response body (not attachment)
        /// </summary>
        /// <param name="ev"></param>
        /// <param name="code">Status code</param>
        /// <param name="file">The <see cref="FileStream"/> of the desired file to attach</param>
        /// <exception cref="ContentTypeUnacceptableException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CloseResponse(this IHttpEvent ev, HttpStatusCode code, FileStream file)
        {
            ArgumentNullException.ThrowIfNull(ev);
            ArgumentNullException.ThrowIfNull(file);

            //Get content type from filename
            ContentType ct = HttpHelpers.GetContentTypeFromFile(file.Name);            
            //Set the input as a stream
            ev.CloseResponse(code, ct, file, file.Length);
        }


        /// <summary>
        /// Close a response to a connection with a character buffer using the server wide
        /// <see cref="ConnectionInfo.Encoding"/> encoding
        /// </summary>
        /// <param name="ev"></param>
        /// <param name="code">The response status code</param>
        /// <param name="type">The <see cref="ContentType"/> the data represents</param>
        /// <param name="data">The character buffer to send</param>
        /// <remarks>This method will store an encoded copy as a memory stream, so be careful with large buffers</remarks>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="ContentTypeUnacceptableException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CloseResponse(this IHttpEvent ev, HttpStatusCode code, ContentType type, ReadOnlySpan<char> data) =>
            //Get a memory stream using server built-in encoding
            CloseResponse(ev, code, type, data, ev.Server.Encoding);

        /// <summary>
        /// Close a response to a connection with a character buffer using the specified encoding type
        /// </summary>
        /// <param name="ev"></param>
        /// <param name="code">The response status code</param>
        /// <param name="type">The <see cref="ContentType"/> the data represents</param>
        /// <param name="data">The character buffer to send</param>
        /// <param name="encoding">The encoding type to use when converting the buffer</param>
        /// <remarks>This method will store an encoded copy as a memory stream, so be careful with large buffers</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CloseResponse(this IHttpEvent ev, HttpStatusCode code, ContentType type, ReadOnlySpan<char> data, Encoding encoding)
        {
            ArgumentNullException.ThrowIfNull(ev);

            if (data.IsEmpty)
            {
                ev.CloseResponse(code);
                return;
            }

            //Validate encoding
            ArgumentNullException.ThrowIfNull(encoding, nameof(encoding));
            
            //Get new simple memory response
            IMemoryResponseReader reader = new SimpleMemoryResponse(data, encoding);
            ev.CloseResponse(code, type, reader);
        }
        
        /// <summary>
        /// Close a response to a connection by copying the speciifed binary buffer
        /// </summary>
        /// <param name="ev"></param>
        /// <param name="code">The response status code</param>
        /// <param name="type">The <see cref="ContentType"/> the data represents</param>
        /// <param name="data">The binary buffer to send</param>
        /// <remarks>The data paramter is copied into an internal <see cref="IMemoryResponseReader"/></remarks>
        /// <exception cref="NotSupportedException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="ContentTypeUnacceptableException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CloseResponse(this IHttpEvent ev, HttpStatusCode code, ContentType type, ReadOnlySpan<byte> data)
        {
            ArgumentNullException.ThrowIfNull(ev);

            if (data.IsEmpty)
            {
                ev.CloseResponse(code);
                return;
            }

            //Get new simple memory response
            IMemoryResponseReader reader = new SimpleMemoryResponse(data);
            ev.CloseResponse(code, type, reader);
        }

        /// <summary>
        /// Close a response to a connection with a relative file within the current root's directory
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="code">The status code to set the response as</param>
        /// <param name="filePath">The path of the relative file to send</param>
        /// <returns>True if the file was found, false if the file does not exist or cannot be accessed</returns>
        /// <exception cref="IOException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="ContentTypeUnacceptableException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CloseWithRelativeFile(this HttpEntity entity, HttpStatusCode code, string filePath)
        {
            ArgumentNullException.ThrowIfNull(entity);
            ArgumentNullException.ThrowIfNull(filePath);

            //See if file exists and is within the root's directory
            if (entity.RequestedRoot.FindResourceInRoot(filePath, out string realPath))
            {
                //get file-info
                FileInfo realFile = new(realPath);
                //Close the response with the file stream
                entity.CloseResponse(code, realFile);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Redirects a client using the specified <see cref="RedirectType"/>
        /// </summary>
        /// <param name="ev"></param>
        /// <param name="type">The <see cref="RedirectType"/> redirection type</param>
        /// <param name="location">Location to direct client to, sets the "Location" header</param>
        /// <remarks>Sets required headers for redirection, disables cache control, and returns the status code to the client</remarks>
        /// <exception cref="UriFormatException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Redirect(this IHttpEvent ev, RedirectType type, string location) 
            => Redirect(ev, type, new Uri(location, UriKind.RelativeOrAbsolute));

        /// <summary>
        /// Redirects a client using the specified <see cref="RedirectType"/>
        /// </summary>
        /// <param name="ev"></param>
        /// <param name="type">The <see cref="RedirectType"/> redirection type</param>
        /// <param name="location">Location to direct client to, sets the "Location" header</param>
        /// <remarks>Sets required headers for redirection, disables cache control, and returns the status code to the client</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Redirect(this IHttpEvent ev, RedirectType type, Uri location)
        {
            ArgumentNullException.ThrowIfNull(ev);
            ArgumentNullException.ThrowIfNull(location);
            
            if(type == RedirectType.None)
            {
                throw new ArgumentException("Invalid redirect type of none", nameof(type));
            }

            //Encode the string for propery http url formatting and set the location header
            ev.Server.Headers[HttpResponseHeader.Location] = location.ToString();
            ev.Server.SetNoCache();

            //Set redirect the ressponse redirect code type
            ev.CloseResponse((HttpStatusCode)type);
        }

        #endregion

        /// <summary>
        /// Attempts to read and deserialize a JSON object from the reqeust body (form data or urlencoded)
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="ev"></param>
        /// <param name="key">Request argument key (name)</param>
        /// <param name="obj"></param>
        /// <returns>true if the argument was found and successfully converted to json</returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="NotSupportedException"></exception>
        /// <exception cref="InvalidJsonRequestException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetJsonFromArg<T>(this IHttpEvent ev, string key, out T? obj) => TryGetJsonFromArg(ev, key, SR_OPTIONS, out obj);
        
        /// <summary>
        /// Attempts to read and deserialize a JSON object from the reqeust body (form data or urlencoded)
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="ev"></param>
        /// <param name="key">Request argument key (name)</param>
        /// <param name="options"><see cref="JsonSerializerOptions"/> to use during deserialization </param>
        /// <param name="obj"></param>
        /// <returns>true if the argument was found and successfully converted to json</returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="NotSupportedException"></exception>
        /// <exception cref="InvalidJsonRequestException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetJsonFromArg<T>(this IHttpEvent ev, string key, JsonSerializerOptions options, out T? obj)
        {
            ArgumentNullException.ThrowIfNull(ev);

            //Check for key in argument
            if (ev.RequestArgs.TryGetNonEmptyValue(key, out string? value))
            {
                try
                {
                    //Deserialize and return the object
                    obj = JsonSerializer.Deserialize<T>(value, options);
                    return true;
                }
                catch(JsonException je)
                {
                    throw new InvalidJsonRequestException(je);
                }
            }
            obj = default;
            return false;
        }
        
        /// <summary>
        /// Reads the value stored at the key location in the request body arguments, into a <see cref="JsonDocument"/>
        /// </summary>
        /// <param name="ev"></param>
        /// <param name="key">Request argument key (name)</param>
        /// <param name="options"><see cref="JsonDocumentOptions"/> to use during parsing</param>
        /// <returns>A new <see cref="JsonDocument"/> if the key is found, null otherwise</returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="InvalidJsonRequestException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static JsonDocument? GetJsonFromArg(this IHttpEvent ev, string key, in JsonDocumentOptions options = default)
        {
            ArgumentNullException.ThrowIfNull(ev);

            try
            {
                //Check for key in argument
                return ev.RequestArgs.TryGetNonEmptyValue(key, out string? value) ? JsonDocument.Parse(value, options) : null;
            }
            catch (JsonException je)
            {
                throw new InvalidJsonRequestException(je);
            }
        }

        /// <summary>
        /// If there are file attachements (form data files or content body) and the file is <see cref="ContentType.Json"/>
        /// file. It will be deserialzied to the specified object
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="ev"></param>
        /// <param name="uploadIndex">The index within <see cref="HttpEntity.Files"/></param> list of the file to read
        /// <param name="options"><see cref="JsonSerializerOptions"/> to use during deserialization </param>
        /// <returns>Returns the deserialized object if found, default otherwise</returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="NotSupportedException"></exception>
        /// <exception cref="InvalidJsonRequestException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T? GetJsonFromFile<T>(this IHttpEvent ev, JsonSerializerOptions? options = null, int uploadIndex = 0)
        {
            ArgumentNullException.ThrowIfNull(ev);

            if (ev.Files.Count <= uploadIndex)
            {
                return default;
            }

            FileUpload file = ev.Files[uploadIndex];
            //Make sure the file is a json file
            if (file.ContentType != ContentType.Json)
            {
                return default;
            }
            try
            {
                //Beware this will buffer the entire file object before it attmepts to de-serialize it
                return JsonSerializer.Deserialize<T>(file.FileData, options);
            }
            catch (JsonException je)
            {
                throw new InvalidJsonRequestException(je);
            }
        }
        
        /// <summary>
        /// If there are file attachements (form data files or content body) and the file is <see cref="ContentType.Json"/>
        /// file. It will be parsed into a new <see cref="JsonDocument"/>
        /// </summary>
        /// <param name="ev"></param>
        /// <param name="uploadIndex">The index within <see cref="HttpEntity.Files"/></param> list of the file to read
        /// <returns>Returns the parsed <see cref="JsonDocument"/>if found, default otherwise</returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="NotSupportedException"></exception>
        /// <exception cref="InvalidJsonRequestException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static JsonDocument? GetJsonFromFile(this IHttpEvent ev, int uploadIndex = 0)
        {
            ArgumentNullException.ThrowIfNull(ev);

            if (ev.Files.Count <= uploadIndex)
            {
                return default;
            }
            FileUpload file = ev.Files[uploadIndex];
            //Make sure the file is a json file
            if (file.ContentType != ContentType.Json)
            {
                return default;
            }
            try
            {
                return JsonDocument.Parse(file.FileData);
            }
            catch(JsonException je)
            {
                throw new InvalidJsonRequestException(je);
            }
        }
        
        /// <summary>
        /// If there are file attachements (form data files or content body) and the file is <see cref="ContentType.Json"/>
        /// file. It will be deserialzied to the specified object
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="ev"></param>
        /// <param name="uploadIndex">The index within <see cref="HttpEntity.Files"/></param> list of the file to read
        /// <param name="options"><see cref="JsonSerializerOptions"/> to use during deserialization </param>
        /// <returns>The deserialized object if found, default otherwise</returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="NotSupportedException"></exception>
        /// <exception cref="IndexOutOfRangeException"></exception>
        /// <exception cref="InvalidJsonRequestException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueTask<T?> GetJsonFromFileAsync<T>(this HttpEntity ev, JsonSerializerOptions? options = null, int uploadIndex = 0)
        {
            ArgumentNullException.ThrowIfNull(ev);

            if (ev.Files.Count <= uploadIndex)
            {
                return ValueTask.FromResult<T?>(default);
            }
            FileUpload file = ev.Files[uploadIndex];
            //Make sure the file is a json file
            if (file.ContentType != ContentType.Json)
            {
                return ValueTask.FromResult<T?>(default);
            }
            //avoid copying the ev struct, so return deserialze task
            static async ValueTask<T?> Deserialze(Stream data, JsonSerializerOptions? options, CancellationToken token)
            {
                try
                {
                    //Beware this will buffer the entire file object before it attmepts to de-serialize it
                    return await VnEncoding.JSONDeserializeFromBinaryAsync<T?>(data, options, token);
                }
                catch (JsonException je)
                {
                    throw new InvalidJsonRequestException(je);
                }
            }
            return Deserialze(file.FileData, options, ev.EventCancellation);
        }
        
        static readonly Task<JsonDocument?> DocTaskDefault = Task.FromResult<JsonDocument?>(null);
        
        /// <summary>
        /// If there are file attachements (form data files or content body) and the file is <see cref="ContentType.Json"/>
        /// file. It will be parsed into a new <see cref="JsonDocument"/>
        /// </summary>
        /// <param name="ev"></param>
        /// <param name="uploadIndex">The index within <see cref="HttpEntity.Files"/></param> list of the file to read
        /// <returns>Returns the parsed <see cref="JsonDocument"/>if found, default otherwise</returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="NotSupportedException"></exception>
        /// <exception cref="IndexOutOfRangeException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task<JsonDocument?> GetJsonFromFileAsync(this HttpEntity ev, int uploadIndex = 0)
        {
            ArgumentNullException.ThrowIfNull(ev);

            if (ev.Files.Count <= uploadIndex)
            {
                return DocTaskDefault;
            }
            FileUpload file = ev.Files[uploadIndex];
            //Make sure the file is a json file
            if (file.ContentType != ContentType.Json)
            {
                return DocTaskDefault;
            }
            static async Task<JsonDocument?> Deserialze(Stream data, CancellationToken token)
            {
                try
                {
                    //Beware this will buffer the entire file object before it attmepts to de-serialize it
                    return await JsonDocument.ParseAsync(data, cancellationToken: token);
                }
                catch (JsonException je)
                {
                    throw new InvalidJsonRequestException(je);
                }
            }
            return Deserialze(file.FileData, ev.EventCancellation);
        }
        
        /// <summary>
        /// If there are file attachements (form data files or content body) the specified parser will be called to parse the 
        /// content body asynchronously into a .net object or its default if no attachments are included
        /// </summary>
        /// <param name="ev"></param>
        /// <param name="parser">A function to asynchronously parse the entity body into its object representation</param>
        /// <param name="uploadIndex">The index within <see cref="HttpEntity.Files"/></param> list of the file to read
        /// <returns>Returns the parsed <typeparamref name="T"/> if found, default otherwise</returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="IndexOutOfRangeException"></exception>
        public static Task<T?> ParseFileAsAsync<T>(this IHttpEvent ev, Func<Stream, Task<T?>> parser, int uploadIndex = 0)
        {
            ArgumentNullException.ThrowIfNull(ev);
            ArgumentNullException.ThrowIfNull(parser);

            if (ev.Files.Count <= uploadIndex)
            {
                return Task.FromResult<T?>(default);
            }
            //Get the file
            FileUpload file = ev.Files[uploadIndex];
            return parser(file.FileData);
        }
        
        /// <summary>
        /// If there are file attachements (form data files or content body) the specified parser will be called to parse the 
        /// content body asynchronously into a .net object or its default if no attachments are included
        /// </summary>
        /// <param name="ev"></param>
        /// <param name="parser">A function to asynchronously parse the entity body into its object representation</param>
        /// <param name="uploadIndex">The index within <see cref="HttpEntity.Files"/></param> list of the file to read
        /// <returns>Returns the parsed <typeparamref name="T"/> if found, default otherwise</returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="IndexOutOfRangeException"></exception>
        public static Task<T?> ParseFileAsAsync<T>(this IHttpEvent ev, Func<Stream, string, Task<T?>> parser, int uploadIndex = 0)
        {
            ArgumentNullException.ThrowIfNull(ev);
            ArgumentNullException.ThrowIfNull(parser);

            if (ev.Files.Count <= uploadIndex)
            {
                return Task.FromResult<T?>(default);
            }
            //Get the file
            FileUpload file = ev.Files[uploadIndex];
            //Parse the file using the specified parser
            return parser(file.FileData, file.ContentTypeString());
        }
        
        /// <summary>
        /// If there are file attachements (form data files or content body) the specified parser will be called to parse the 
        /// content body asynchronously into a .net object or its default if no attachments are included
        /// </summary>
        /// <param name="ev"></param>
        /// <param name="parser">A function to asynchronously parse the entity body into its object representation</param>
        /// <param name="uploadIndex">The index within <see cref="HttpEntity.Files"/></param> list of the file to read
        /// <returns>Returns the parsed <typeparamref name="T"/> if found, default otherwise</returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="IndexOutOfRangeException"></exception>
        public static ValueTask<T?> ParseFileAsAsync<T>(this IHttpEvent ev, Func<Stream, ValueTask<T?>> parser, int uploadIndex = 0)
        {
            ArgumentNullException.ThrowIfNull(ev);
            ArgumentNullException.ThrowIfNull(parser);

            if (ev.Files.Count <= uploadIndex)
            {
                return ValueTask.FromResult<T?>(default);
            }
            //Get the file
            FileUpload file = ev.Files[uploadIndex];
            return parser(file.FileData);
        }
        
        /// <summary>
        /// If there are file attachements (form data files or content body) the specified parser will be called to parse the 
        /// content body asynchronously into a .net object or its default if no attachments are included
        /// </summary>
        /// <param name="ev"></param>
        /// <param name="parser">A function to asynchronously parse the entity body into its object representation</param>
        /// <param name="uploadIndex">The index within <see cref="HttpEntity.Files"/></param> list of the file to read
        /// <returns>Returns the parsed <typeparamref name="T"/> if found, default otherwise</returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="IndexOutOfRangeException"></exception>
        public static ValueTask<T?> ParseFileAsAsync<T>(this IHttpEvent ev, Func<Stream, string, ValueTask<T?>> parser, int uploadIndex = 0)
        {
            ArgumentNullException.ThrowIfNull(ev);
            ArgumentNullException.ThrowIfNull(parser);

            if (ev.Files.Count <= uploadIndex)
            {
                return ValueTask.FromResult<T?>(default);
            }
            //Get the file
            FileUpload file = ev.Files[uploadIndex];
            //Parse the file using the specified parser
            return parser(file.FileData, file.ContentTypeString());
        }
        
        /// <summary>
        /// Get a <see cref="DirectoryInfo"/> instance that points to the current sites filesystem root.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="PathTooLongException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DirectoryInfo GetRootDir(this HttpEntity ev) => new(ev.RequestedRoot.Options.Directory);
        
        /// <summary>
        /// Returns the MIME string representation of the content type of the uploaded file.
        /// </summary>
        /// <param name="upload"></param>
        /// <returns>The MIME string representation of the content type of the uploaded file.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string ContentTypeString(this in FileUpload upload) => HttpHelpers.GetContentTypeString(upload.ContentType);

        /// <summary>
        /// Sets the <see cref="HttpControlMask.CompressionDisabled"/> flag on the current 
        /// <see cref="IHttpEvent"/> instance to disable dynamic compression on the response.
        /// </summary>
        /// <param name="entity"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DisableCompression(this IHttpEvent entity) => entity.SetControlFlag(HttpControlMask.CompressionDisabled);

        /// <summary>
        /// Attempts to upgrade the connection to a websocket, if the setup fails, it sets up the response to the client accordingly.
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="socketOpenedCallback">A delegate that will be invoked when the websocket has been opened by the framework</param>
        /// <param name="subProtocol">The sub-protocol to use on the current websocket</param>
        /// <param name="userState">An object to store in the <see cref="WebSocketSession{T}.UserState"/> property when the websocket has been accepted</param>
        /// <param name="keepAlive">An optional, explicit web-socket keep-alive interval</param>
        /// <returns>True if operation succeeds.</returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        public static bool AcceptWebSocket<T>(this IHttpEvent entity, 
            WebSocketAcceptedCallback<T> socketOpenedCallback, 
            T userState, 
            string? subProtocol = null, 
            TimeSpan keepAlive = default
            )
        {
            //Must define an accept callback
            ArgumentNullException.ThrowIfNull(socketOpenedCallback);
            
            if (PrepWebSocket(entity, subProtocol))
            {
                //Set a default keep alive if none was specified
                if (keepAlive == default)
                {
                    keepAlive = TimeSpan.FromSeconds(30);
                }

                IAlternateProtocol ws = new WebSocketSession<T>(GetNewSocketId(), socketOpenedCallback)
                {
                    SubProtocol = subProtocol,
                    IsSecure = entity.Server.IsSecure(),
                    UserState = userState,
                    KeepAlive = keepAlive,
                };

                //Setup a new websocket session with a new session id
                entity.DangerousChangeProtocol(ws);

                return true;
            }

            return false;
        }

        /// <summary>
        /// Attempts to upgrade the connection to a websocket, if the setup fails, it sets up the response to the client accordingly.
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="socketOpenedCallback">A delegate that will be invoked when the websocket has been opened by the framework</param>
        /// <param name="subProtocol">The sub-protocol to use on the current websocket</param>
        /// <param name="keepAlive">An optional, explicit web-socket keep-alive interval</param>
        /// <returns>True if operation succeeds.</returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        public static bool AcceptWebSocket(this IHttpEvent entity, WebSocketAcceptedCallback socketOpenedCallback, string? subProtocol = null, TimeSpan keepAlive = default)
        {
            //Must define an accept callback
            ArgumentNullException.ThrowIfNull(entity);
            ArgumentNullException.ThrowIfNull(socketOpenedCallback);          

            if(PrepWebSocket(entity, subProtocol))
            {
                //Set a default keep alive if none was specified
                if (keepAlive == default)
                {
                    keepAlive = TimeSpan.FromSeconds(30);
                }

                IAlternateProtocol ws = new WebSocketSession(GetNewSocketId(), socketOpenedCallback)
                {
                    SubProtocol = subProtocol,
                    IsSecure = entity.Server.IsSecure(),
                    KeepAlive = keepAlive,
                };

                //Setup a new websocket session with a new session id
                entity.DangerousChangeProtocol(ws);

                return true;
            }

            return false;
        }

        private static string GetNewSocketId() => Guid.NewGuid().ToString("N");

        private static bool PrepWebSocket(this IHttpEvent entity, string? subProtocol = null)
        {
            ArgumentNullException.ThrowIfNull(entity);

            //Make sure this is a websocket request
            if (!entity.Server.IsWebSocketRequest)
            {
                throw new InvalidOperationException("Connection is not a websocket request");
            }

            string? version = entity.Server.Headers["Sec-WebSocket-Version"];

            //rfc6455:4.2, version must equal 13
            if (!string.IsNullOrWhiteSpace(version) && version.Contains("13", StringComparison.OrdinalIgnoreCase))
            {
                //Get socket key
                string? key = entity.Server.Headers["Sec-WebSocket-Key"];
                if (!string.IsNullOrWhiteSpace(key) && key.Length < 25)
                {
                    //Set headers for acceptance
                    entity.Server.Headers[HttpResponseHeader.Upgrade] = "websocket";
                    entity.Server.Headers[HttpResponseHeader.Connection] = "Upgrade";

                    //Hash accept string
                    entity.Server.Headers["Sec-WebSocket-Accept"] = ManagedHash.ComputeHash($"{key.Trim()}{HttpHelpers.WebsocketRFC4122Guid}", HashAlg.SHA1, HashEncodingMode.Base64);

                    //Protocol if user specified it
                    if (!string.IsNullOrWhiteSpace(subProtocol))
                    {
                        entity.Server.Headers["Sec-WebSocket-Protocol"] = subProtocol;
                    }

                    return true;
                }
            }
            //Set the client up for a bad request response, nod a valid websocket request
            entity.CloseResponse(HttpStatusCode.BadRequest);
            return false;
        }
    }
}