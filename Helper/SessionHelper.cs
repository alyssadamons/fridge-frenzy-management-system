using Newtonsoft.Json;

namespace E_Commerce.Helper
{
    public static class SessionHelper
    {
        public static void SetObjectAsJson(this ISession session, string key, object value)
        {
            session.SetString(key, System.Text.Json.JsonSerializer.Serialize(value));
        }
        public static T GetObjectFromJson<T>(this ISession session, string key)
        {
            var value = session.GetString(key);
            return value == null ? default : Newtonsoft.Json.JsonConvert.DeserializeObject<T>(value);
        }
    }
}
