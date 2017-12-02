﻿using System.Collections.Concurrent;
using Xeora.Web.Basics.Context;
using Xeora.Web.Site.Service;

namespace Xeora.Web.Handler
{
    public class HandlerManager
    {
        private ConcurrentDictionary<string, HandlerContainer> _Handlers;

        private HandlerManager() =>
            this._Handlers = new ConcurrentDictionary<string, HandlerContainer>();

        private static HandlerManager _Current = null;
        public static HandlerManager Current
        {
            get
            {
                if (HandlerManager._Current == null)
                    HandlerManager._Current = new HandlerManager();

                return HandlerManager._Current;
            }
        }

        public Basics.IHandler Create(ref IHttpContext context)
        {
            // TODO: Pool Variable Expire minutes should me dynamic
            PoolFactory.Initialize(20);

            Basics.IHandler handler =
                new XeoraHandler(ref context);

            this.Add(ref handler);

            return handler;
        }

        public Basics.IHandler Get(string handlerID)
        {
            if (string.IsNullOrEmpty(handlerID))
                return null;

            HandlerContainer handlerContainer;
            if (!this._Handlers.TryGetValue(handlerID, out handlerContainer))
                return null;

            return handlerContainer.Handler;
        }

        private void Add(ref Basics.IHandler handler)
        {
            if (handler == null)
                return;

            HandlerContainer handlerContainer =
                new HandlerContainer(ref handler);

            this._Handlers.AddOrUpdate(handler.HandlerID, handlerContainer, (cHandlerID, cHandlerContainer) => handlerContainer);
        }

        public void Mark(string handlerID)
        {
            HandlerContainer handlerContainer;
            if (!this._Handlers.TryGetValue(handlerID, out handlerContainer))
                return;

            handlerContainer.Removable = false;
        }

        public void UnMark(string handlerID)
        {
            HandlerContainer handlerContainer;
            if (!this._Handlers.TryGetValue(handlerID, out handlerContainer))
                return;

            if (handlerContainer.Removable)
                this._Handlers.TryRemove(handlerID, out handlerContainer);
            else
                handlerContainer.Removable = true;
        }
    }
}
