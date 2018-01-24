﻿using System;

namespace Xeora.Web.Basics.Execution
{
    [Serializable()]
    public class Bind
    {
        private Bind(Context.HttpMethod httpMethod, string executable, string[] classes, string procedure, string[] parameters)
        {
            this.HttpMethod = httpMethod;
            this.Executable = executable;
            this.Classes = classes;
            this.Procedure = procedure;
            this.Parameters = new ProcedureParameterCollection(parameters);
            this.InstanceExecution = false;
        }

        /// <summary>
        /// Gets or sets the request http method
        /// </summary>
        /// <value>The request http method</value>
        public Context.HttpMethod HttpMethod { get; private set; }

        /// <summary>
        /// Gets the name of the xeora executable
        /// </summary>
        /// <value>The name of the xeora executable</value>
        public string Executable { get; private set; }

        /// <summary>
        /// Gets the class tree from top to bottom
        /// </summary>
        /// <value>The class tree</value>
        public string[] Classes { get; private set; }

        /// <summary>
        /// Gets the name of the procedure
        /// </summary>
        /// <value>The name of the procedure</value>
        public string Procedure { get; private set; }

        /// <summary>
        /// Gets the procedure parameters
        /// </summary>
        /// <value>The procedure parameters</value>
        public ProcedureParameterCollection Parameters { get; private set; }

        /// <summary>
        /// Gets a value indicating whether this <see cref="T:Xeora.Web.Basics.Execution.Bind"/> is ready
        /// </summary>
        /// <value><c>true</c> if is ready; otherwise, <c>false</c></value>
        public bool Ready => this.Parameters.Healthy;

        /// <summary>
        /// Gets or sets a value indicating whether this <see cref="T:Xeora.Web.Basics.Execution.Bind"/>
        /// instance execution. If the class requires instance creation, make it <c>true</c>
        /// </summary>
        /// <value><c>true</c> if instance execution; otherwise, <c>false</c></value>
        public bool InstanceExecution { get; set; }

        /// <summary>
        /// Make the Bind from string
        /// </summary>
        /// <returns>The Bind</returns>
        /// <param name="bind">Bind string</param>
        public static Bind Make(string bind)
        {
            if (!string.IsNullOrEmpty(bind))
            {
                try
                {
                    string[] splittedBind1 = bind.Split('?');

                    if (splittedBind1.Length == 2)
                    {
                        string executable = splittedBind1[0];
                        string[] splittedBind2 = splittedBind1[1].Split(',');

                        string[] classes = null;
                        string procedure = null;

                        string[] classProcSearch = splittedBind2[0].Split('.');

                        if (classProcSearch.Length == 1)
                        {
                            classes = null;
                            procedure = classProcSearch[0];
                        }
                        else
                        {
                            classes = new string[classProcSearch.Length - 1];
                            Array.Copy(classProcSearch, 0, classes, 0, classes.Length);

                            procedure = classProcSearch[classProcSearch.Length - 1];
                        }

                        string[] parameters = null;
                        if (splittedBind2.Length > 1)
                            parameters = string.Join(",", splittedBind2, 1, splittedBind2.Length - 1).Split('|');

                        return new Bind(Helpers.Context.Request.Header.Method, executable, classes, procedure, parameters);
                    }
                }
                catch (Exception)
                {
                    // Just Handle Exceptions
                }
            }

            return null;
        }

        /// <summary>
        /// Returns a <see cref="T:System.String"/> that represents the current <see cref="T:Xeora.Web.Basics.Execution.Bind"/>
        /// </summary>
        /// <returns>A <see cref="T:System.String"/> that represents the current <see cref="T:Xeora.Web.Basics.Execution.Bind"/></returns>
        public override string ToString()
        {
            return 
                string.Format("{0}?{1}{2}{3}{4}",
                    this.Executable,
                    string.Join(".", this.Classes),
                    (this.Classes == null ? string.Empty : "."),
                    this.Procedure,
                    this.Parameters.ToString()
                );
        }

        /// <summary>
        /// Clone into the specified bind
        /// </summary>
        /// <param name="bind">Bind object that keeps cloned data</param>
        public void Clone(out Bind bind) =>
            bind = new Bind(this.HttpMethod, this.Executable, this.Classes, this.Procedure, this.Parameters.Queries);
    }
}
