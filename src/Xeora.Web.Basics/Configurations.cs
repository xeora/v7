﻿namespace Xeora.Web.Basics
{
    public class Configurations
    {
        private static Configuration.IXeora _Xeora;
        /// <summary>
        /// Gets the Xeora framework configurations
        /// </summary>
        /// <value>Xeora framework configuration instance</value>
        public static Configuration.IXeora Xeora =>
            Configurations._Xeora ?? (Configurations._Xeora = Helpers.Negotiator.XeoraSettings);
    }
}
