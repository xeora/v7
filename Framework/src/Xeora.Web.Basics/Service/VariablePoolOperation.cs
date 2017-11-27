﻿using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Runtime.Serialization.Formatters.Binary;
using System.Collections.Concurrent;

namespace Xeora.Web.Basics.Service
{
    [CLSCompliant(true)]
    public sealed class VariablePoolOperation
    {
        private static IVariablePool _Cache = null;

        private string _SessionID;
        private string _KeyID;
        private string _SessionKeyID;

        public VariablePoolOperation(string sessionID, string keyID)
        {
            if (VariablePoolOperation._Cache == null)
            {
                try
                {
                    VariablePoolOperation._Cache =
                        (IVariablePool)TypeCache.Instance.RemoteInvoke.InvokeMember("GetVariablePool", BindingFlags.Public | BindingFlags.Static | BindingFlags.InvokeMethod, null, null, new object[] { sessionID });
                }
                catch (Exception ex)
                {
                    throw new TargetInvocationException("Communication Error! Variable Pool is not accessable...", ex);
                }
            }

            this._SessionID = sessionID;
            this._KeyID = keyID;
            this._SessionKeyID = string.Format("{0}_{1}", sessionID, keyID);
        }

        public void Set(string name, object value)
        {
            if (!string.IsNullOrWhiteSpace(name) && name.Length > 128)
                throw new ArgumentOutOfRangeException(nameof(name), "Key must not be longer than 128 characters!");

            if (value == null)
            {
                this.UnRegisterVariableFromPool(name);

                return;
            }

            this.RegisterVariableToPool(name, value);
        }

        public object Get(string name) =>
            this.GetVariableFromPool(name);

        public T Get<T>(string name)
        {
            object objectValue = this.Get(name);

            if (objectValue is T)
                return (T)objectValue;

            return default(T);
        }

        public void Transfer(string fromSessionID) =>
            this.TransferRegistrations(string.Format("{0}_{1}", fromSessionID, this._KeyID));

        private object GetVariableFromPool(string name)
        {
            object rObject = VariablePoolPreCache.GetCachedVariable(this._SessionKeyID, name);

            if (rObject == null)
            {
                byte[] serializedValue = VariablePoolOperation._Cache.Get(this._SessionKeyID, name);

                if (serializedValue != null)
                {
                    Stream forStream = null;

                    try
                    {
                        BinaryFormatter binFormater = new BinaryFormatter();
                        binFormater.Binder = new OverrideBinder();

                        forStream = new MemoryStream(serializedValue);

                        rObject = binFormater.Deserialize(forStream);

                        VariablePoolPreCache.CacheVariable(this._SessionKeyID, name, rObject);
                    }
                    catch (Exception)
                    {
                        // Just Handle Exceptions
                    }
                    finally
                    {
                        if (forStream != null)
                        {
                            forStream.Close();
                            GC.SuppressFinalize(forStream);
                        }
                    }
                }
            }

            return rObject;
        }

        private void RegisterVariableToPool(string name, object value)
        {
            VariablePoolPreCache.CleanCachedVariables(this._SessionKeyID, name);

            byte[] serializedValue = new byte[] { };
            Stream forStream = null;
            try
            {
                forStream = new MemoryStream();

                BinaryFormatter binFormater = new BinaryFormatter();
                binFormater.Serialize(forStream, value);

                serializedValue = ((MemoryStream)forStream).ToArray();
            }
            catch (Exception)
            {
                // Just Handle Exceptions
            }
            finally
            {
                if (forStream != null)
                {
                    forStream.Close();
                    GC.SuppressFinalize(forStream);
                }
            }

            VariablePoolOperation._Cache.Set(this._SessionKeyID, name, serializedValue);
        }

        private void UnRegisterVariableFromPool(string name)
        {
            VariablePoolPreCache.CleanCachedVariables(this._SessionKeyID, name);

            // Unregister Variable From Pool Immidiately. 
            // Otherwise it will cause cache reload in the same domain call
            VariablePoolOperation._Cache.Set(this._SessionKeyID, name, null);
        }

        private void TransferRegistrations(string fromSessionID)
        {
            try
            {
                VariablePoolOperation._Cache =
                    (IVariablePool)TypeCache.Instance.RemoteInvoke.InvokeMember("TransferVariablePool", BindingFlags.Public | BindingFlags.Static | BindingFlags.InvokeMethod, null, null, new object[] { fromSessionID, this._SessionID });
            }
            catch (Exception ex)
            {
                throw new TargetInvocationException("Communication Error! Variable Pool is not accessable...", ex);
            }
        }

        private byte[] SerializeNameValuePairs(ConcurrentDictionary<string, object> nameValuePairs)
        {
            SerializableDictionary serializableDictionary = new SerializableDictionary();

            byte[] serializedValue = new byte[] { };

            if (nameValuePairs == null)
                return serializedValue;

            Stream forStream = null;
            foreach (string variableName in nameValuePairs.Keys)
            {
                forStream = null;
                try
                {
                    object variableValue;
                    if (nameValuePairs.TryGetValue(variableName, out variableValue))
                    {
                        forStream = new MemoryStream();

                        BinaryFormatter binFormater = new BinaryFormatter();
                        binFormater.Serialize(forStream, variableValue);

                        serializedValue = ((MemoryStream)forStream).ToArray();

                        serializableDictionary.Add(new SerializableDictionary.SerializableKeyValuePair(variableName, serializedValue));
                    }
                }
                catch (Exception)
                {
                    // Just Handle Exceptions
                }
                finally
                {
                    if (forStream != null)
                    {
                        forStream.Close();
                        GC.SuppressFinalize(forStream);
                    }
                }
            }

            forStream = null;
            try
            {
                forStream = new MemoryStream();

                BinaryFormatter binFormater = new BinaryFormatter();
                binFormater.Serialize(forStream, serializableDictionary);

                return ((MemoryStream)forStream).ToArray();
            }
            catch (Exception)
            {
                return new byte[] { };
            }
            finally
            {
                if (forStream != null)
                {
                    forStream.Close();
                    GC.SuppressFinalize(forStream);
                }
            }
        }

        // This class required to eliminate the mass request to VariablePool.
        // VariablePool registration requires serialization...
        // Use PreCache for only read keys do not use for variable registration!
        // It is suitable for repeating requests...
        private class VariablePoolPreCache
        {
            private static ConcurrentDictionary<string, ConcurrentDictionary<string, object>> _VariablePreCache = null;
            public static ConcurrentDictionary<string, ConcurrentDictionary<string, object>> VariablePreCache
            {
                get
                {
                    if (VariablePoolPreCache._VariablePreCache == null)
                        VariablePoolPreCache._VariablePreCache = new ConcurrentDictionary<string, ConcurrentDictionary<string, object>>();

                    return VariablePoolPreCache._VariablePreCache;
                }
            }

            public static object GetCachedVariable(string sessionKeyID, string name)
            {
                ConcurrentDictionary<string, object> nameValuePairs = null;
                if (VariablePoolPreCache.VariablePreCache.TryGetValue(sessionKeyID, out nameValuePairs))
                {
                    object value = null;
                    if (nameValuePairs.TryGetValue(name, out value) && value != null)
                        return value;
                }

                return null;
            }

            public static void CacheVariable(string sessionKeyID, string name, object value)
            {
                ConcurrentDictionary<string, object> nameValuePairs = null;
                if (!VariablePoolPreCache.VariablePreCache.TryGetValue(sessionKeyID, out nameValuePairs))
                {
                    nameValuePairs = new ConcurrentDictionary<string, object>();

                    if (!VariablePoolPreCache.VariablePreCache.TryAdd(sessionKeyID, nameValuePairs))
                    {
                        VariablePoolPreCache.CacheVariable(sessionKeyID, name, value);

                        return;
                    }
                }

                if (value == null)
                    nameValuePairs.TryRemove(name, out value);
                else
                    nameValuePairs.AddOrUpdate(name, value, (cName, cValue) => value);
            }

            public static void CleanCachedVariables(string sessionKeyID, string name)
            {
                ConcurrentDictionary<string, object> nameValuePairs = null;
                if (VariablePoolPreCache.VariablePreCache.TryGetValue(sessionKeyID, out nameValuePairs))
                {
                    object dummy;
                    nameValuePairs.TryRemove(name, out dummy);
                }
            }
        }

        [CLSCompliant(true), Serializable()]
        public class SerializableDictionary : List<SerializableDictionary.SerializableKeyValuePair>
        {
            [Serializable()]
            public class SerializableKeyValuePair
            {
                public SerializableKeyValuePair(string name, byte[] value)
                {
                    this.Name = name;
                    this.Value = value;
                }

                public string Name { get; private set; }
                public byte[] Value { get; private set; }
            }
        }
    }
}