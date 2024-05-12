/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Rest.Client
* File: Extensions.cs 
*
* Extensions.cs is part of VNLib.Net.Rest.Client which is part of 
* the larger VNLib collection of libraries and utilities.
*
* VNLib.Net.Rest.Client is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.Net.Rest.Client is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with VNLib.Net.Rest.Client. If not, see http://www.gnu.org/licenses/.
*/

using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

using RestSharp;

namespace VNLib.Net.Rest.Client.Construction
{
    /// <summary>
    /// Construction extensions
    /// </summary>
    public static class Extensions
    {
        /// <summary>
        /// Executes a request against the site by sending the request model parameter. An <see cref="IRestEndpointAdapter{TModel}"/> must be 
        /// defined to handle requests of the given model type.
        /// </summary>
        /// <typeparam name="TModel"></typeparam>
        /// <param name="site"></param>
        /// <param name="entity">The request entity model to send to the server</param>
        /// <param name="cancellation">A token to cancel the operation</param>
        /// <returns>A task that resolves the response message</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static async Task<RestResponse> ExecuteAsync<TModel>(this IRestSiteAdapter site, TModel entity, CancellationToken cancellation = default)
        {
            ArgumentNullException.ThrowIfNull(site);

            //Get the adapter for the model
            IRestEndpointAdapter<TModel> adapter = site.GetAdapter<TModel>();

            //Get new request on adapter
            RestRequest request = adapter.GetRequest(entity);

            //Wait to exec operations if needed
            await site.WaitAsync(cancellation);

            RestResponse response;

            //Get rest client
            using (ClientContract contract = site.GetClient())
            {
                //Exec response
                response = await contract.Resource.ExecuteAsync(request, cancellation);
            }

            //Site handler should not cause an exception
            site.OnResponse(response);

            //invoke response handlers
            adapter.OnResponse(entity, response);

            return response;
        }

        /// <summary>
        /// Begins a stream download of the desired resource by sending the request model parameter. 
        /// An <see cref="IRestEndpointAdapter{TModel}"/> must be defined to handle requests of the given model type.
        /// <para>
        /// WARNING: This function will not invoke the OnResponse handler functions after the stream 
        /// has been returned, there is no way to inspect the response when excuting a stream download
        /// </para>
        /// </summary>
        /// <typeparam name="TModel"></typeparam>
        /// <param name="site"></param>
        /// <param name="entity">The request entity model to send to the server</param>
        /// <param name="cancellation">A token to cancel the operation</param>
        /// <returns>A task that resolves the response data stream</returns>
        public static async Task<Stream?> DownloadStreamAsync<TModel>(this IRestSiteAdapter site, TModel entity, CancellationToken cancellation = default)
        {
            ArgumentNullException.ThrowIfNull(site);

            //Get the adapter for the model
            IRestEndpointAdapter<TModel> adapter = site.GetAdapter<TModel>();

            //Get new request on adapter
            RestRequest request = adapter.GetRequest(entity);

            //Wait to exec operations if needed
            await site.WaitAsync(cancellation);

            Stream? response;

            //Get rest client
            using (ClientContract contract = site.GetClient())
            {
                //Exec response
                response = await contract.Resource.DownloadStreamAsync(request, cancellation);
            }

            return response;
        }

        /// <summary>
        /// Executes a request against the site by sending the request model parameter. An <see cref="IRestEndpointAdapter{TModel}"/> must be 
        /// defined to handle requests of the given model type.
        /// </summary>
        /// <typeparam name="TModel"></typeparam>
        /// <typeparam name="TJson">The response entity type</typeparam>
        /// <param name="site"></param>
        /// <param name="entity">The request entity model to send to the server</param>
        /// <param name="cancellation">A token to cancel the operation</param>
        /// <returns>A task that resolves the response message with json resonse support</returns>
        public static async Task<RestResponse<TJson>> ExecuteAsync<TModel, TJson>(this IRestSiteAdapter site, TModel entity, CancellationToken cancellation = default)
        {
            ArgumentNullException.ThrowIfNull(site);

            //Get the adapter for the model
            IRestEndpointAdapter<TModel> adapter = site.GetAdapter<TModel>();

            //Get new request on adapter
            RestRequest request = adapter.GetRequest(entity);

            //Wait to exec operations if needed
            await site.WaitAsync(cancellation);

            RestResponse<TJson> response;

            //Get rest client
            using (ClientContract contract = site.GetClient())
            {
                //Exec response
                response = await contract.Resource.ExecuteAsync<TJson>(request, cancellation);
            }

            //Site handler should not cause an exception
            site.OnResponse(response);

            //invoke response handlers
            adapter.OnResponse(entity, response);

            return response;
        }

        /// <summary>
        /// Executes a request using a model that defines its own endpoint information, on the current site and returns the response
        /// </summary>
        /// <typeparam name="TModel">The request model type</typeparam>
        /// <param name="site"></param>
        /// <param name="model">The entity model that defines itself as an endpoint and the request information</param>
        /// <param name="cancellation">A token to cancel the operation</param>
        /// <returns>When completed, gets the <see cref="RestResponse"/></returns>
        public static async Task<RestResponse> ExecuteSingleAsync<TModel>(this IRestSiteAdapter site, TModel model, CancellationToken cancellation = default) where TModel : IRestSingleEndpoint
        {
            ArgumentNullException.ThrowIfNull(site);

            //Init new request
            RestRequest request = new(model.Url, model.Method);
            model.OnRequest(request);

            //Wait to exec operations if needed
            await site.WaitAsync(cancellation);

            RestResponse response;

            //Get rest client
            using (ClientContract contract = site.GetClient())
            {
                //Exec response
                response = await contract.Resource.ExecuteAsync(request, cancellation);
            }

            //Site handler should not cause an exception
            site.OnResponse(response);

            //Allow model to handle a response
            model.OnResponse(response);

            return response;
        }


        /// <summary>
        /// Sets the request method of a new request
        /// </summary>
        /// <typeparam name="TModel"></typeparam>
        /// <param name="builder"></param>
        /// <param name="methodCb">The callback method that will be invoked on every call to build a new request</param>
        /// <returns>The chainable <see cref="IRestRequestBuilder{TModel}"/></returns>
        public static IRestRequestBuilder<TModel> WithMethod<TModel>(this IRestRequestBuilder<TModel> builder, Func<TModel, Method> methodCb)
        {
            builder.WithModifier((m, r) => methodCb(m));
            return builder;
        }

        /// <summary>
        /// Sets a constant request method of a new request
        /// </summary>
        /// <typeparam name="TModel"></typeparam>
        /// <param name="builder"></param>
        /// <param name="method">The constant request method</param>
        /// <returns>The chainable <see cref="IRestRequestBuilder{TModel}"/></returns>
        public static IRestRequestBuilder<TModel> WithMethod<TModel>(this IRestRequestBuilder<TModel> builder, Method method)
        {
            //Set constant method value
            builder.WithModifier((m, r) => r.Method = method);
            return builder;
        }

        /// <summary>
        /// Sets a callback that will create a query string argument value
        /// </summary>
        /// <typeparam name="TModel">The request entity type</typeparam>
        /// <param name="builder"></param>
        /// <param name="callback">The callback method that gets the query value</param>
        /// <param name="parameter">The query paramter value to set</param>
        /// <returns>The chainable <see cref="IRestRequestBuilder{TModel}"/></returns>
        public static IRestRequestBuilder<TModel> WithQuery<TModel>(this IRestRequestBuilder<TModel> builder, string parameter, Func<TModel, string> callback)
        {
            //Get a query item string value from the callback and sets the query paremter
            builder.WithModifier((m, r) => r.AddQueryParameter(parameter, callback(m)));
            return builder;
        }

        /// <summary>
        /// Sets a callback that will create a query string argument value
        /// </summary>
        /// <typeparam name="TModel">The request entity type</typeparam>
        /// <param name="builder"></param>
        /// <param name="value">The constant query value</param>
        /// <param name="parameter">The name of the query paramter to set</param>
        /// <returns>The chainable <see cref="IRestRequestBuilder{TModel}"/></returns>
        public static IRestRequestBuilder<TModel> WithQuery<TModel>(this IRestRequestBuilder<TModel> builder, string parameter, string value)
        {
            //Get a query item string value from the callback and sets the query paremter
            builder.WithModifier((m, r) => r.AddQueryParameter(parameter, value));
            return builder;
        }

        /// <summary>
        /// Specifies a model that will handle its own request body builder
        /// </summary>
        /// <typeparam name="TModel"></typeparam>
        /// <param name="builder"></param>
        /// <returns>The chainable <see cref="IRestRequestBuilder{TModel}"/></returns>
        public static IRestRequestBuilder<TModel> WithBodyBuilder<TModel>(this IRestRequestBuilder<TModel> builder) where TModel : IRestRequestBody
        {
            builder.WithModifier(static (m, r) => m.AddBody(r));
            return builder;
        }

        /// <summary>
        /// Builds endpoints from an <see cref="IRestEndpointDefinition"/> and stores them in the 
        /// <see cref="IRestSiteEndpointStore"/>
        /// </summary>
        /// <param name="site"></param>
        /// <param name="endpoint">The endpoint definition to build endpoints from</param>
        /// <returns>A chainable <see cref="IRestSiteEndpointStore"/></returns>
        public static IRestSiteEndpointStore BuildEndpoints(this IRestSiteEndpointStore site, IRestEndpointDefinition endpoint)
        {
            EndpointAdapterBuilder builder = new(site);

            //Build endpoints
            endpoint.BuildRequest(site.Site, builder);

            return site;
        }

        /// <summary>
        /// Allows building of a single endpoint from an <see cref="IRestEndpointDefinition"/> and stores it in the
        /// adapter store.
        /// </summary>
        /// <param name="site"></param>
        /// <returns>A chainable <see cref="IRestEndpointBuilder"/> to build the endpoint</returns>
        public static IRestEndpointBuilder DefineSingleEndpoint(this IRestSiteEndpointStore site)
        {
            EndpointAdapterBuilder builder = new(site);
            return builder;
        }

        /// <summary>
        /// Sets the uri for all new request messages
        /// </summary>
        /// <typeparam name="TModel">The request entity type</typeparam>
        /// <param name="builder"></param>
        /// <param name="withUri">Specifies the uri for the request builder</param>
        /// <returns>The chainable <see cref="IRestRequestBuilder{TModel}"/></returns>
        public static IRestRequestBuilder<TModel> WithUrl<TModel>(this IRestRequestBuilder<TModel> builder, Func<TModel, Uri> withUri)
        {
            //Use the supplied method to convert the uri to a string
            builder.WithUrl((m) => withUri(m).ToString());
            return builder;
        }


        /// <summary>
        /// Sets the uri for all new request messages
        /// </summary>
        /// <typeparam name="TModel">The request entity type</typeparam>
        /// <param name="builder"></param>
        /// <param name="uri">Specifies the uri for the request builder</param>
        /// <returns>The chainable <see cref="IRestRequestBuilder{TModel}"/></returns>
        public static IRestRequestBuilder<TModel> WithUrl<TModel>(this IRestRequestBuilder<TModel> builder, Uri uri)
        {
            //Use the supplied method to convert the uri to a string
            builder.WithUrl((m) => uri.ToString());
            return builder;
        }

        /// <summary>
        /// Sets the uri for all new request messages
        /// </summary>
        /// <typeparam name="TModel">The request entity type</typeparam>
        /// <param name="builder"></param>
        /// <param name="url">Specifies the uri for the request builder</param>
        /// <returns>The chainable <see cref="IRestRequestBuilder{TModel}"/></returns>
        public static IRestRequestBuilder<TModel> WithUrl<TModel>(this IRestRequestBuilder<TModel> builder, string url)
        {
            //Use the supplied method to convert the uri to a string
            builder.WithUrl((m) => url);
            return builder;
        }

        /// <summary>
        /// Specifies a connection header to set for every request
        /// </summary>
        /// <typeparam name="TModel">The request entity type</typeparam>
        /// <param name="builder"></param>
        /// <param name="header">The name of the header to set</param>
        /// <param name="func">The callback function to get the request header value</param>
        /// <returns>The chainable <see cref="IRestRequestBuilder{TModel}"/></returns>
        public static IRestRequestBuilder<TModel> WithHeader<TModel>(this IRestRequestBuilder<TModel> builder, string header, Func<TModel, string> func)
        {
            //Specify a header by the given name
            builder.WithModifier((m, req) => req.AddHeader(header, func(m)));
            return builder;
        }

        /// <summary>
        /// Specifies a static connection header to set for every request
        /// </summary>
        /// <typeparam name="TModel">The request entity type</typeparam>
        /// <param name="builder"></param>
        /// <param name="header">The name of the header to set</param>
        /// <param name="value">The static header value to set for all requests</param>
        /// <returns>The chainable <see cref="IRestRequestBuilder{TModel}"/></returns>
        public static IRestRequestBuilder<TModel> WithHeader<TModel>(this IRestRequestBuilder<TModel> builder, string header, string value)
        {
            //Specify a header by the given name
            builder.WithModifier((m, req) => req.AddHeader(header, value));
            return builder;
        }

        /// <summary>
        /// Sets a callback that will create a parameter string argument value
        /// </summary>
        /// <typeparam name="TModel">The request entity type</typeparam>
        /// <param name="builder"></param>
        /// <param name="callback">The callback method that gets the query value</param>
        /// <param name="parameter">The body paramter value to set</param>
        /// <returns>The chainable <see cref="IRestRequestBuilder{TModel}"/></returns>
        public static IRestRequestBuilder<TModel> WithParameter<TModel>(this IRestRequestBuilder<TModel> builder, string parameter, Func<TModel, string> callback)
        {
            //Get a query item string value from the callback and sets the query paremter
            builder.WithModifier((m, r) => r.AddParameter(parameter, callback(m), ParameterType.GetOrPost));
            return builder;
        }

        /// <summary>
        /// Sets a constant parameter string argument value
        /// </summary>
        /// <typeparam name="TModel">The request entity type</typeparam>
        /// <param name="builder"></param>
        /// <param name="value">The constant value</param>
        /// <param name="parameter">The query paramter value to set</param>
        /// <returns>The chainable <see cref="IRestRequestBuilder{TModel}"/></returns>
        public static IRestRequestBuilder<TModel> WithParameter<TModel>(this IRestRequestBuilder<TModel> builder, string parameter, string value)
        {
            //Get a query item string value from the callback and sets the query paremter
            builder.WithModifier((m, r) => r.AddParameter(parameter, value, ParameterType.GetOrPost));
            return builder;
        }


        /// <summary>
        /// Converts a task that resolves a <see cref="RestResponse"/> to a task that deserializes 
        /// the response data as json.
        /// </summary>
        /// <typeparam name="TResult">The json response entity type</typeparam>
        /// <param name="response">The response task</param>
        /// <returns>A task that resolves the deserialized entity type</returns>
        public static Task<TResult?> AsJson<TResult>(this Task<RestResponse> response) 
            => As(response, static r => JsonSerializer.Deserialize<TResult>(r.RawBytes));

        /// <summary>
        /// Converts a task that resolves a <see cref="RestResponse"/> to a task that deserializes 
        /// the response data as json.
        /// </summary>
        /// <param name="response">The response task</param>
        /// <returns>A task that resolves the deserialized entity type</returns>
        public static Task<byte[]?> AsBytes(this Task<RestResponse> response) => As(response, static p => p.RawBytes);

        /// <summary>
        /// Converts a task that resolves a <see cref="RestResponse"/> to a task that uses your
        /// transformation function to create the result
        /// </summary>
        /// <param name="response">The response task</param>
        /// <param name="callback">Your custom callback function used to transform the data</param>
        /// <returns>A task that resolves the deserialized entity type</returns>
        public static async Task<T> As<T>(this Task<RestResponse> response, Func<RestResponse, Task<T>> callback)
        {
            ArgumentNullException.ThrowIfNull(response);
            ArgumentNullException.ThrowIfNull(callback);

            RestResponse r = await response.ConfigureAwait(false);
            return await callback(r).ConfigureAwait(false);
        }

        /// <summary>
        /// Converts a task that resolves a <see cref="RestResponse"/> to a task that uses your
        /// transformation function to create the result
        /// </summary>
        /// <param name="response">The response task</param>
        /// <param name="callback">Your custom callback function used to transform the data</param>
        /// <returns>A task that resolves the deserialized entity type</returns>
        public static async Task<T> As<T>(this Task<RestResponse> response, Func<RestResponse, T> callback)
        {
            ArgumentNullException.ThrowIfNull(response);
            ArgumentNullException.ThrowIfNull(callback);

            RestResponse r = await response.ConfigureAwait(false);
            return callback(r);
        }

        private record class EndpointAdapterBuilder(IRestSiteEndpointStore Site) : IRestEndpointBuilder
        {
            ///<inheritdoc/>
            public IRestRequestBuilder<TModel> WithEndpoint<TModel>()
            {
                //New adapter
                EndpointAdapter<TModel> adapter = new();

                //Store adapter in site
                Site.AddAdapter(adapter);

                //Builder 
                return new RequestBuilder<TModel>(adapter);
            }

            private record class RequestBuilder<TModel>(EndpointAdapter<TModel> Adapter) : IRestRequestBuilder<TModel>
            {
                ///<inheritdoc/>
                public IRestRequestBuilder<TModel> WithModifier(Action<TModel, RestRequest> requestBuilder)
                {
                    ArgumentNullException.ThrowIfNull(requestBuilder);
                   
                    Adapter.RequestChain.AddLast(requestBuilder);
                    return this;
                }

                ///<inheritdoc/>
                public IRestRequestBuilder<TModel> WithUrl(Func<TModel, string> uriBuilder)
                {
                    ArgumentNullException.ThrowIfNull(uriBuilder);

                    Adapter.GetUrl = uriBuilder;
                    return this;
                }

                ///<inheritdoc/>
                public IRestRequestBuilder<TModel> OnResponse(Action<TModel, RestResponse> onResponseBuilder)
                {
                    ArgumentNullException.ThrowIfNull(onResponseBuilder);

                    Adapter.ResponseChain.AddLast(onResponseBuilder);
                    return this;
                }
            }

            private sealed class EndpointAdapter<TModel> : IRestEndpointAdapter<TModel>
            {
                internal Func<TModel, string>? GetUrl { get; set; }

                internal LinkedList<Action<TModel, RestRequest>> RequestChain { get; } = new();
                internal LinkedList<Action<TModel, RestResponse>> ResponseChain { get; } = new();

                RestRequest IRestEndpointAdapter<TModel>.GetRequest(TModel entity)
                {
                    //First we need to get the url for the entity
                    string? url = GetUrl?.Invoke(entity);

                    //New request
                    RestRequest request = new(url);

                    //Invoke request modifier chain
                    foreach (Action<TModel, RestRequest> action in RequestChain)
                    {
                        action(entity, request);
                    }

                    return request;
                }

                void IRestEndpointAdapter<TModel>.OnResponse(TModel model, RestResponse response)
                {
                    //Invoke request modifier chain
                    foreach (Action<TModel, RestResponse> action in ResponseChain)
                    {
                        action(model, response);
                    }
                }
            }
        }

    }
}
