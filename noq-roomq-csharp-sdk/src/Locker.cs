using NoQ.RoomQ.Exception;
using System.Collections.Generic;
using System.Net;
using static NoQ.RoomQ.ManagedHttpClient;
using System.Text.Json;


namespace NoQ.RoomQ
{
    public class Locker
    {
        private readonly string clientID;
        private readonly string token;
        private readonly string apiKey;
        private readonly ManagedHttpClient HttpClient;

        public Locker(string clientID, string apiKey, string token, string url)
        {
            this.clientID = clientID;
            this.apiKey = apiKey;
            this.token = token;
            this.HttpClient = new ManagedHttpClient(url);
        }

        /**
        * @throws InvalidApiKeyException|System.Net.Http.HttpClientException
        */
        public JsonElement FindSessions(string key, string value)
        {
            try
            {
                string url = "/api/lockers/" + UrlEncode(this.clientID) + "/sessions";
                Dictionary<string, string> headers = new Dictionary<string, string>() { { "Api-Key", this.apiKey } };
                Dictionary<string, string> query = new Dictionary<string, string>() { { "key", key }, { "value", value } };
                Response response = this.HttpClient.Get(url, query: query, headers: headers);
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                    throw new InvalidApiKeyException();
                else if (!response.IsSuccess)
                    throw new System.Exception(response.Content);
                return response.GetDeserializedContent<JsonElement>().GetProperty("sessions");
            }
            catch (InvalidApiKeyException e) { throw e; }
            catch (System.Exception e) { throw e; }
        }

        /**
        * @throws System.Net.Http.HttpClientException
        * @throws InvalidApiKeyException
        */
        public JsonElement Fetch()
        {
            try
            {
                string url = "/api/lockers/" + UrlEncode(this.clientID) + "/sessions/" + UrlEncode(this.token);
                Dictionary<string, string> headers = new Dictionary<string, string>() { { "Api-Key", this.apiKey } };
                Response response = this.HttpClient.Get(url, headers: headers);
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                    throw new InvalidApiKeyException();
                else if (!response.IsSuccess)
                    throw new System.Exception(response.Content);
                return response.GetDeserializedContent<JsonElement>();
            }
            catch (InvalidApiKeyException e) { throw e; }
            catch (System.Exception e) { throw e; }
        }

        /**
        * @param LockerItem[] items
        * @param int expireAt
        * @throws ReachLimitException|InvalidApiKeyException|System.Net.Http.HttpClientException
        */
        public void Put(LockerItem[] items, int expireAt)
        {
            try
            {
                string url = "/api/lockers/" + UrlEncode(this.clientID) + "/sessions/" + UrlEncode(this.token);
                Dictionary<string, string> headers = new Dictionary<string, string>() { { "Api-Key", this.apiKey } };
                Dictionary<string, object> payload = new Dictionary<string, object>()
                {
                    {"data", items },
                    {"expireAt", expireAt }
                };
                Response response = this.HttpClient.Put(url, payload: payload, headers: headers);
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                    throw new InvalidApiKeyException();
                else if (response.StatusCode == HttpStatusCode.Forbidden)
                    throw new ReachLimitException();
                else if (!response.IsSuccess)
                    throw new System.Exception(response.Content);
            }
            catch (InvalidApiKeyException e) { throw e; }
            catch (ReachLimitException e) { throw e; }
            catch (System.Exception e) { throw e; }
        }

        /**
        * @param string key
        * @throws System.Net.Http.HttpClientException
        * @throws InvalidApiKeyException
        */
        public void Delete(string key)
        {
            try
            {
                string url = "/api/lockers/" + UrlEncode(this.clientID) + "/sessions/" + UrlEncode(this.token) + "/" + key;
                Dictionary<string, string> headers = new Dictionary<string, string>() { { "Api-Key", this.apiKey } };
                Response response = this.HttpClient.Delete(url, headers: headers);
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                    throw new InvalidApiKeyException();
                else if (response.StatusCode == HttpStatusCode.Forbidden)
                    throw new ReachLimitException();
                else if (!response.IsSuccess)
                    throw new System.Exception(response.Content);
            }
            catch (InvalidApiKeyException e) { throw e; }
            catch (System.Exception e) { throw e; }
        }


        /**
        * @throws System.Net.Http.HttpClientException
        * @throws InvalidApiKeyException
        */
        public void Flush()
        {
            try
            {
                string url = "/api/lockers/" + UrlEncode(this.clientID) + "/sessions/" + UrlEncode(this.token);
                Dictionary<string, string> headers = new Dictionary<string, string>() { { "Api-Key", this.apiKey } };
                Response response = this.HttpClient.Delete(url, headers: headers);
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                    throw new InvalidApiKeyException();
                else if (response.StatusCode == HttpStatusCode.Forbidden)
                    throw new ReachLimitException();
                else if (!response.IsSuccess)
                    throw new System.Exception(response.Content);
            }
            catch (InvalidApiKeyException e) { throw e; }
            catch (System.Exception e) { throw e; }
        }
    }
}
