using System;
using System.Text;

namespace HttpServer.Authentication
{
    /// <summary>
    /// The "basic" authentication scheme is based on the model that the
   /// client must authenticate itself with a user-ID and a password for
   /// each realm.  The realm value should be considered an opaque string
   /// which can only be compared for equality with other realms on that
   /// server. The server will service the request only if it can validate
   /// the user-ID and password for the protection space of the Request-URI.
    /// There are no optional authentication parameters.
    /// </summary>
    public class BasicAuthentication : AuthModule
    {
        /// <summary>
        /// Create a response that can be sent in the WWW-Authenticate header.
        /// </summary>
        /// <param name="realm">Realm that the user should authenticate in</param>
        /// <param name="options">Not used in basic auth</param>
        /// <returns>A correct auth request.</returns>
        public override string CreateResponse(string realm, params object[] options)
        {
            if (string.IsNullOrEmpty(realm))
                throw new ArgumentNullException("realm");

            return "Basic realm=\"" + realm + "\"";
        }

        /// <summary>
        /// An authentication response have been received from the web browser.
        /// Check if it's correct
        /// </summary>
        /// <param name="authenticationHeader">Contents from the Authorization header</param>
        /// <param name="realm">Realm that should be authenticated</param>
        /// <param name="httpVerb">GET/POST/PUT/DELETE etc.</param>
        /// <param name="options">Not used in basic auth</param>
        /// <returns>Authentication object that is stored for the request. A user class or something like that.</returns>
        /// <exception cref="ArgumentException">if authenticationHeader is invalid</exception>
        /// <exception cref="ArgumentNullException">If any of the paramters is empty or null.</exception>
        public override object Authenticate(string authenticationHeader, string realm, string httpVerb,
                                            params object[] options)
        {
            if (string.IsNullOrEmpty(authenticationHeader))
                throw new ArgumentNullException("realm");
            if (authenticationHeader.Length < 20)
                throw new ArgumentException("To small authentication header, can not contain a valid md5 string.", "authenticationHeader");
            if (string.IsNullOrEmpty(realm))
                throw new ArgumentNullException("realm");
            if (string.IsNullOrEmpty(httpVerb))
                throw new ArgumentNullException("httpVerb");

            /*
             * To receive authorization, the client sends the userid and password,
      separated by a single colon (":") character, within a base64 [7]
      encoded string in the credentials.*/
            authenticationHeader = authenticationHeader.Remove(0, 6);
            string decoded = Encoding.UTF8.GetString(Convert.FromBase64String(authenticationHeader));
            int pos = decoded.IndexOf(':');
            if (pos == -1)
                return null;

            string ourPw = decoded.Substring(pos + 1, decoded.Length - pos - 1);
            string pw = ourPw;
            object state;
            CheckAuthentication(realm, decoded.Substring(0, pos), ref pw, out state);
            if (ourPw == pw)
                return state;
            else
                return null;
        }

        /// <summary>
        /// name used in http request.
        /// </summary>
        public override string Name
        {
            get { return "basic"; }
        }
    }
}