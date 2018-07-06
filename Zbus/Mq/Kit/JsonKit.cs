using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;

namespace zbus
{

    public static class JsonKit
    {
        public static JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            //TypeNameHandling = TypeNameHandling.Objects,
        };

        public static string SerializeObject(object value)
        {
            return JsonConvert.SerializeObject(value, JsonSettings);
        }

        public static T DeserializeObject<T>(string value)
        {
            return JsonConvert.DeserializeObject<T>(value, JsonSettings);
        }

        public static object Convert(object raw, Type type)
        {
            if (raw == null)
            {
                return null;
            }

            if (type == typeof(void)) return null;

            if (raw.GetType().IsAssignableFrom(type)) return raw;

            string jsonRaw;
            if (raw is string)
            {
                jsonRaw = (string)raw;
            }
            else
            {
                jsonRaw = JsonConvert.SerializeObject(raw);
            }

            return JsonConvert.DeserializeObject(jsonRaw, type, JsonSettings);
        }

        public static T Convert<T>(object raw)
        {
            if (raw == null)
            {
                return default(T);
            }

            Type type = typeof(T);
            if (type == typeof(void)) return default(T);

            if (raw.GetType().IsAssignableFrom(type)) return (T)raw;

            string jsonRaw;
            if (raw is string)
            {
                jsonRaw = (string)raw;
            }
            else
            {
                jsonRaw = JsonConvert.SerializeObject(raw);
            }
            return JsonConvert.DeserializeObject<T>(jsonRaw, JsonSettings);
        }
    }
}