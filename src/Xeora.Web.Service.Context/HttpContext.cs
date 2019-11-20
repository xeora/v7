﻿using System;
using Xeora.Web.Service.Application;
using Xeora.Web.Service.Session;

namespace Xeora.Web.Service.Context
{
    public class HttpContext : KeyValueCollection<string, object>, Basics.Context.IHttpContext
    {
        private readonly Basics.Session.IHttpSession _Session;

        public HttpContext(string contextId, ref Basics.Context.IHttpRequest request)
        {
            string sessionCookieKey = Basics.Configurations.Xeora.Session.CookieKey;

            this.UniqueId = contextId;
            this.HashCode = 
                this.GetOrCreateHashCode(ref request);
            this.Request = request;
            this.Response = new HttpResponse(contextId);

            string sessionId = string.Empty;
            Basics.Context.IHttpCookieInfo sessionIdCookie =
                this.Request.Header.Cookie[sessionCookieKey];
            if (sessionIdCookie != null)
            {
                sessionId = sessionIdCookie.Value;

                // Remove sessionCookie from the request object
                ((HttpCookie)this.Request.Header.Cookie).Remove(sessionCookieKey);
            }

            SessionManager.Current.Acquire(
                sessionId,
                out this._Session);

            ((HttpResponse)this.Response).SessionCookieRequested +=
                skip =>
                {
                    if (skip)
                        return null;

                    if (string.CompareOrdinal(sessionId, this._Session.SessionId) == 0)
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

        private string GetOrCreateHashCode(ref Basics.Context.IHttpRequest request)
        {
            string requestFilePath =
                request.Header.Url.RelativePath;

            int biIndex = 
                requestFilePath.IndexOf(Basics.Configurations.Xeora.Application.Main.ApplicationRoot.BrowserImplementation, StringComparison.InvariantCulture);
            if (biIndex > -1)
                requestFilePath = requestFilePath.Remove(0, biIndex + Basics.Configurations.Xeora.Application.Main.ApplicationRoot.BrowserImplementation.Length);

            System.Text.RegularExpressions.Match mR =
                System.Text.RegularExpressions.Regex.Match(requestFilePath, "\\d+/");

            if (mR.Success && mR.Index == 0)
                return mR.Value.Substring(0, mR.Length - 1);
            
            return this.GetHashCode().ToString().Replace("-", string.Empty);
        }
        
        public string UniqueId { get; }
        public Basics.Context.IHttpRequest Request { get; }
        public Basics.Context.IHttpResponse Response { get; }
        public Basics.Session.IHttpSession Session => this._Session;
        public Basics.Application.IHttpApplication Application { get; }

        public new void AddOrUpdate(string key, object value) =>
            base.AddOrUpdate(key, value);
        
        public string HashCode { get; }

        public void Dispose()
        {
            ((HttpResponse)this.Response).Dispose();
            ((HttpRequest)this.Request).Dispose();
        }
    }
}
