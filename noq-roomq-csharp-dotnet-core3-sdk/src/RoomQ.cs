using System;
using Jose;
using static NoQ.RoomQ.ManagedHttpClient;
using Microsoft.AspNetCore.Http;
using NoQ.RoomQ.Exception;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Collections.Generic;

namespace NoQ.RoomQ
{
    public class RoomQ
    {
        private readonly string clientID;
        private readonly string jwtSecret;
        private readonly string ticketIssuer;
        private readonly bool debug;
        private readonly string tokenName;
        private string token;
        private readonly string statusEndpoint;

        public RoomQ(string clientID, string jwtSecret, string ticketIssuer, string statusEndpoint, HttpContext httpContext = null, bool debug = false)
        {
            this.clientID = clientID;
            this.jwtSecret = jwtSecret;
            this.ticketIssuer = ticketIssuer;
            this.debug = debug;
            this.statusEndpoint = statusEndpoint;
            this.tokenName = "be_roomq_t_" + this.clientID;
            this.token = this.GetToken(httpContext);
        }

        /**
         * @return string|null
         */
        public string GetToken(HttpContext httpContext = null)
        {
            string token = string.Empty;
            if (httpContext != null)
            {
                HttpRequest httpRequest = httpContext.Request;
                if (httpRequest.Query.ContainsKey("noq_t"))
                {
                    token = httpRequest.Query["noq_t"];
                }
                else if (httpRequest.Cookies.ContainsKey(this.tokenName))
                {
                    token = httpRequest.Cookies[this.tokenName];
                }
            }
            return token;
        }

        public ValidationResult Validate(HttpContext httpContext, string returnUrl, string sessionId)
        {
            string token = this.token;
            var request = httpContext.Request;
            string currentUrl = request.Scheme + "://" + request.Host + request.Path + request.QueryString;
            bool needGenerateJWT = false;
            bool needRedirect = false;

            if (token == null)
            {
                needGenerateJWT = true;
                needRedirect = true;
                this.DebugPrint("no jwt");
            }
            else
            {
                this.DebugPrint("current jwt " + token);
                try
                {
                    // TODO: leeway logic is missing
                    var secret = Encoding.UTF8.GetBytes(this.jwtSecret);
                    JsonElement data = JWT.Decode<JsonElement>(token, secret, JwsAlgorithm.HS256);
                    if (sessionId != null && data.TryGetProperty("session_id", out _) && data.GetProperty("session_id").GetString() != sessionId)
                    {
                        needGenerateJWT = true;
                        needRedirect = true;
                        this.DebugPrint("session id not match");
                    }
                    else if (data.TryGetProperty("deadline", out _) && DateTimeOffset.FromUnixTimeSeconds(data.GetProperty("deadline").GetInt64()) < DateTimeOffset.Now)
                    {
                        needRedirect = true;
                        this.DebugPrint("deadline exceed");
                    }
                    else if (data.GetProperty("type").GetString() == "queue")
                    {
                        needRedirect = true;
                        this.DebugPrint("in queue");
                    }
                    else if (data.GetProperty("type").GetString() == "self-sign")
                    {
                        needRedirect = true;
                        this.DebugPrint("self sign token");
                    }
                }
                catch (System.Exception e)
                {
                    needGenerateJWT = true;
                    needRedirect = true;
                    this.DebugPrint("invalid secret");
                }
            }
            if (needGenerateJWT)
            {
                token = this.GenerateJWT(sessionId);
                this.DebugPrint("generating new jwt token");
                this.token = token;
            }
            CookieOptions option = new CookieOptions();
            option.Expires = DateTimeOffset.Now.AddSeconds(12 * 60 * 60);
            httpContext.Response.Cookies.Append(this.tokenName, token, option);

            if (needRedirect)
            {
                return this.RedirectToTicketIssuer(token, returnUrl ?? currentUrl);
            }
            else
            {
                return this.Enter(currentUrl);
            }
        }

        public Locker GetLocker(string apiKey, string url)
        {
            return new Locker(this.clientID, apiKey, this.token, url);
        }

        public void Extend(ref HttpContext httpContext, int duration)
        {
            string backend = this.GetBackend();
            try
            {
                ManagedHttpClient client = new ManagedHttpClient("https://" + backend);
                Dictionary<string, object> payload = new Dictionary<string, object>()
                {
                    {"action", "beep"},
                    {"client_id", this.clientID},
                    {"id", this.token},
                    {"extend_serving_duration", duration * 60 }
                };
                Response response = client.Post("/queue/" + this.clientID, payload: payload);
                if (!response.IsSuccess && response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    throw new InvalidApiKeyException();
                }
                else if (!response.IsSuccess && response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    throw new NotServingException();
                }
                else
                {
                    JsonElement json = response.GetDeserializedContent<JsonElement>();
                    string newToken = json.GetProperty("id").GetString();
                    this.token = newToken;

                    CookieOptions option = new CookieOptions();
                    option.Expires = DateTimeOffset.Now.AddSeconds(12 * 60 * 60);
                    httpContext.Response.Cookies.Append(this.tokenName, this.token, option);
                }
            }
            catch (System.Exception e)
            {
                throw e;
            }
        }

        public long GetServing()
        {
            string backend = this.GetBackend();
            ManagedHttpClient client = new ManagedHttpClient("https://" + backend);
            try
            {
                Response response = client.Get("/rooms/" + this.clientID + "/servings/" + this.token);
                if (!response.IsSuccess && response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    throw new InvalidApiKeyException();
                }
                else if (!response.IsSuccess && response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    throw new NotServingException();
                }
                else
                {
                    JsonElement json = response.GetDeserializedContent<JsonElement>();
                    return json.GetProperty("deadline").GetInt64();
                }
            }
            catch (System.Exception e)
            {
                throw e;
            }
        }

        public void DeleteServing(HttpContext httpContext)
        {
            string backend = this.GetBackend();

            ManagedHttpClient client = new ManagedHttpClient("https://" + backend);
            Dictionary<string, string> payload = new Dictionary<string, string>()
            {
                {"action", "delete_serving"},
                {"client_id", this.clientID},
                {"id", this.token}
            };
            Response response = client.Post("/queue/" + this.clientID, payload: payload);
            Console.WriteLine("[DEBUG]: " + response.Content);

            JsonElement json = response.GetDeserializedContent<JsonElement>();
            // TODO: leeway logic is missing
            var secret = Encoding.UTF8.GetBytes(this.jwtSecret);
            JsonElement data = JWT.Decode<JsonElement>(this.token, secret, JwsAlgorithm.HS256);
            var token = this.GenerateJWT(data.GetProperty("session_id").GetString());
            this.token = token;
            CookieOptions options = new CookieOptions();
            options.Expires = DateTimeOffset.Now.AddSeconds(12 * 60 * 60);
            httpContext.Response.Cookies.Append(this.tokenName, token, options);
        }

        private ValidationResult Enter(string currentUrl)
        {
            string urlWithoutToken = this.RemoveNoQToken(currentUrl);
            // redirect if url contain token
            if (urlWithoutToken != currentUrl)
            {
                return new ValidationResult(urlWithoutToken);
            }
            return new ValidationResult(null);
        }
        
        private ValidationResult RedirectToTicketIssuer(string token, string currentUrl)
        {
            string urlWithoutToken = this.RemoveNoQToken(currentUrl);
            Dictionary<string, string> _params = new Dictionary<string, string>()
            {
                {"noq_t", token },
                {"noq_c", this.clientID},
                {"noq_r", urlWithoutToken}
            };

            return new ValidationResult(this.ticketIssuer + "?" + QueryStringBuilder(_params));
        }

        private string GenerateJWT(string sessionID = null)
        {
            var data = new Dictionary<string, string>()
            {
                { "room_id", this.clientID },
                { "session_id", sessionID ?? Guid.NewGuid().ToString() },
                { "type", "self-sign" }
            };
            var secret = Encoding.ASCII.GetBytes(this.jwtSecret);
            return JWT.Encode(data, secret, JwsAlgorithm.HS256);
        }

        private void DebugPrint(string message)
        {
            if (this.debug)
            {
                Console.WriteLine("[RoomQ] {0}", message);
            }
        }

        private string RemoveNoQToken(string currentUrl)
        {
            string updated = Regex.Replace(currentUrl, "([&]*)(noq_t=[^&]*)", "");
            updated = Regex.Replace(updated, "\\?&", "?");
            return Regex.Replace(updated, "\\?$", "");
        }

        /**
         * TODO
         * @return 
         * @throws 
         * @throws QueueStoppedException
         */
        public string GetBackend()
        {
            ManagedHttpClient client = new ManagedHttpClient(this.statusEndpoint);
            Response response = client.Get("/" + this.clientID);
            JsonElement json = response.GetDeserializedContent<JsonElement>();
            string state = json.GetProperty("state").GetString();
            if (state == "stopped")
            {
                throw new QueueStoppedException();
            }
            string backend = json.GetProperty("backend").GetString();
            return backend;
        }
    }
}
