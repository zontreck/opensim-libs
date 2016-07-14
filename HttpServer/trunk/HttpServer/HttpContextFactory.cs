using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace HttpServer
{
    /// <summary>
    /// Used to create and reuse contexts.
    /// </summary>
    public class HttpContextFactory : IHttpContextFactory
    {
        private readonly int _bufferSize;
        private readonly Queue<HttpClientContext> _contextQueue = new Queue<HttpClientContext>();
        private readonly IRequestParserFactory _factory;
        private readonly ILogWriter _logWriter;
        private readonly ContextTimeoutManager _contextTimeoutManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpContextFactory"/> class.
        /// </summary>
        /// <param name="writer">The writer.</param>
        /// <param name="bufferSize">Amount of bytes to read from the incoming socket stream.</param>
        /// <param name="factory">Used to create a request parser.</param>
        public HttpContextFactory(ILogWriter writer, int bufferSize, IRequestParserFactory factory)
        {
            _logWriter = writer;
            _bufferSize = bufferSize;
            _factory = factory;
            _contextTimeoutManager = new ContextTimeoutManager(ContextTimeoutManager.MonitorType.Thread);
        }

        ///<summary>
        /// True if detailed trace logs should be written.
        ///</summary>
        public bool UseTraceLogs { get; set; }

        /// <summary>
        /// Create a new context.
        /// </summary>
        /// <param name="isSecured">true if socket is running HTTPS.</param>
        /// <param name="endPoint">Client that connected</param>
        /// <param name="stream">Network/SSL stream.</param>
        /// <returns>A context.</returns>
        protected HttpClientContext CreateContext(bool isSecured, IPEndPoint endPoint, Stream stream, Socket sock)
        {
        	HttpClientContext context;
            lock (_contextQueue)
            {
                if (_contextQueue.Count > 0)
                {
                    context = _contextQueue.Dequeue();
                    if (!context.Available)
                    {
                        context = CreateNewContext(isSecured, endPoint, stream, sock);
                        context.Disconnected += OnFreeContext;
                        context.RequestReceived += OnRequestReceived;
                        context.EndWhenDone = true;
                        
                    }
                }
                else
                {
                    context = CreateNewContext(isSecured, endPoint, stream, sock);
                    context.Disconnected += OnFreeContext;
                    context.RequestReceived += OnRequestReceived;
                }
            }

        	context.Stream = stream;
			context.IsSecured = isSecured;
			context.RemotePort = endPoint.Port.ToString();
			context.RemoteAddress = endPoint.Address.ToString();
			_contextTimeoutManager.StartMonitoringContext(context);
            context.Start();

            return context;
        }

		/// <summary>
		/// Create a new context.
		/// </summary>
		/// <param name="isSecured">true if HTTPS is used.</param>
		/// <param name="endPoint">Remote client</param>
		/// <param name="stream">Network stream, <see cref="HttpClientContext"/>
		/// <returns>A new context (always).</returns>
    	protected virtual HttpClientContext CreateNewContext(bool isSecured, IPEndPoint endPoint, Stream stream, Socket sock)
    	{
    		return new HttpClientContext(isSecured, endPoint, stream, _factory, _bufferSize, sock);
    	}

    	private void OnRequestReceived(object sender, RequestEventArgs e)
        {
            RequestReceived(sender, e);
        }

        private void OnFreeContext(object sender, DisconnectedEventArgs e)
        {
            var imp = (HttpClientContext) sender;
            imp.Cleanup();

            if (!imp.EndWhenDone)
            {
                lock (_contextQueue)
                    _contextQueue.Enqueue(imp);
            }
            else
            {
               
                imp.Close();
            }
        }


        #region IHttpContextFactory Members

        /// <summary>
        /// Create a secure <see cref="IHttpClientContext"/>.
        /// </summary>
        /// <param name="socket">Client socket (accepted by the <see cref="HttpListener"/>).</param>
        /// <param name="certificate">HTTPS certificate to use.</param>
        /// <param name="protocol">Kind of HTTPS protocol. Usually TLS or SSL.</param>
        /// <returns>
        /// A created <see cref="IHttpClientContext"/>.
        /// </returns>
        public IHttpClientContext CreateSecureContext(Socket socket, X509Certificate certificate, SslProtocols protocol)
        {
			var networkStream = new NetworkStream(socket, true);
            var remoteEndPoint = (IPEndPoint) socket.RemoteEndPoint;

            var sslStream = new SslStream(networkStream, false);
            try
            {
                //TODO: this may fail
                sslStream.AuthenticateAsServer(certificate, false, protocol, false);
                return CreateContext(true, remoteEndPoint, sslStream, socket);
            }
            catch (IOException err)
            {
                if (UseTraceLogs)
                    _logWriter.Write(this, LogPrio.Trace, err.Message);
            }
            catch (ObjectDisposedException err)
            {
                if (UseTraceLogs)
                    _logWriter.Write(this, LogPrio.Trace, err.Message);
            }

            return null;
        }

		
        /// <summary>
        /// A request have been received from one of the contexts.
        /// </summary>
        public event EventHandler<RequestEventArgs> RequestReceived = delegate{};

        /// <summary>
        /// Creates a <see cref="IHttpClientContext"/> that handles a connected client.
        /// </summary>
        /// <param name="socket">Client socket (accepted by the <see cref="HttpListener"/>).</param>
        /// <returns>
        /// A creates <see cref="IHttpClientContext"/>.
        /// </returns>
        public IHttpClientContext CreateContext(Socket socket)
        {
			var networkStream = new NetworkStream(socket, true);
            var remoteEndPoint = (IPEndPoint) socket.RemoteEndPoint;
            return CreateContext(false, remoteEndPoint, networkStream, socket);
        }

        #endregion

        /// <summary>
        /// Server is shutting down so shut down the factory
        /// </summary>
        public void Shutdown()
        {
            _contextTimeoutManager.StopMonitoring();
        }
    }

    /// <summary>
    /// Used to create <see cref="IHttpClientContext"/>es.
    /// </summary>
    public interface IHttpContextFactory
    {
        /// <summary>
        /// Creates a <see cref="IHttpClientContext"/> that handles a connected client.
        /// </summary>
        /// <param name="socket">Client socket (accepted by the <see cref="HttpListener"/>).</param>
        /// <returns>A creates <see cref="IHttpClientContext"/>.</returns>
        IHttpClientContext CreateContext(Socket socket);

        /// <summary>
        /// Create a secure <see cref="IHttpClientContext"/>.
        /// </summary>
        /// <param name="socket">Client socket (accepted by the <see cref="HttpListener"/>).</param>
        /// <param name="certificate">HTTPS certificate to use.</param>
        /// <param name="protocol">Kind of HTTPS protocol. Usually TLS or SSL.</param>
        /// <returns>A created <see cref="IHttpClientContext"/>.</returns>
        IHttpClientContext CreateSecureContext(Socket socket, X509Certificate certificate, SslProtocols protocol);

        /// <summary>
        /// A request have been received from one of the contexts.
        /// </summary>
        event EventHandler<RequestEventArgs> RequestReceived;

        /// <summary>
        /// Server is shutting down so shut down the factory
        /// </summary>
        void Shutdown();
    }
}