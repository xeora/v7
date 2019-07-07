﻿using Xeora.Web.Service.Application;
using Xeora.Web.Service.Session;

namespace Xeora.Web.Service.Context
{
    public class HttpContext : KeyValueCollection<string, object>, Basics.Context.IHttpContext
    {
        private Basics.Session.IHttpSession _Session;

        public HttpContext(string contextId, ref Basics.Context.IHttpRequest request)
        {
            string sessionCookieKey = Basics.Configurations.Xeora.Session.CookieKey;

            this.Request = request;
            this.Response = new HttpResponse(contextId);

            string sessionId = string.Empty;
            Basics.Context.IHttpCookieInfo sessionIdCookie =
                this.Request.Header.Cookie[sessionCookieKey];
            if (sessionIdCookie != null)
            {
                sessionId = sessionIdCookie.Value;

                // Remove sessioncookie from the request object
                ((HttpCookie)this.Request.Header.Cookie).Remove(sessionCookieKey);
            }

            SessionManager.Current.Acquire(
                sessionId,
                out this._Session);

            ((HttpResponse)this.Response).SessionCookieRequested +=
                (skip) =>
                {
                    if (skip)
                        return null;

                    if (string.Compare(sessionId, this._Session.SessionId) == 0)
                        return null;

                    if (this._Session.Keys.Length == 0)
                        return null;

                    // Create SessionCookie
                    sessionIdCookie =
                        this.Response.Header.Cookie.CreateNewCookie(sessionCookieKey);
                    sessionIdCookie.Value = this._Session.SessionId;
                    sessionIdCookie.HttpOnly = true;

                    return sessionIdCookie;
                };

            this.Application = ApplicationContainer.Current;
        }

        public Basics.Context.IHttpRequest Request { get; private set; }
        public Basics.Context.IHttpResponse Response { get; private set; }
        public Basics.Session.IHttpSession Session => this._Session;
        public Basics.Application.IHttpApplication Application { get; private set; }

        public new void AddOrUpdate(string key, object value) =>
            base.AddOrUpdate(key, value);

        private string _HashCode = string.Empty;
        public string HashCode
        {
            get
            {
                if (!string.IsNullOrEmpty(this._HashCode))
                    return this._HashCode;

                string RequestFilePath =
                    this.Request.Header.URL.RelativePath;

                int biIndex = RequestFilePath.IndexOf(Basics.Configurations.Xeora.Application.Main.ApplicationRoot.BrowserImplementation, System.StringComparison.InvariantCulture);
                if (biIndex > -1)
                    RequestFilePath = RequestFilePath.Remove(0, biIndex + Basics.Configurations.Xeora.Application.Main.ApplicationRoot.BrowserImplementation.Length);

                System.Text.RegularExpressions.Match mR =
                    System.Text.RegularExpressions.Regex.Match(RequestFilePath, "\\d+/");

                if (mR.Success && mR.Index == 0)
                    this._HashCode = mR.Value.Substring(0, mR.Length - 1);
                else
                {
                    this._HashCode = this.GetHashCode().ToString();
                    this._HashCode = this._HashCode.Replace("-", string.Empty);
                }

                return this._HashCode;
            }
        }

        public void Dispose()
        {
            ((HttpResponse)this.Response).Dispose();
            ((HttpRequest)this.Request).Dispose();
        }
    }
}
