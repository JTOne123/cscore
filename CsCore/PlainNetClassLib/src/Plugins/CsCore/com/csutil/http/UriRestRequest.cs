using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using com.csutil.http;

namespace com.csutil.http {
    internal class UriRestRequest : RestRequest {
        private Uri uri;
        public Action<UriRestRequest, HttpResponseMessage> handleResult;
        public IJsonReader jsonReader = JsonReader.NewReader();
        private Task sendTask;

        public UriRestRequest(Uri uri) { this.uri = uri; }

        public Task<T> GetResult<T>(Action<T> successCallback) {
            Task<string> readResultTask = null;
            Func<T> getResult = null;
            handleResult = (self, resp) => {
                using (readResultTask = resp.Content.ReadAsStringAsync()) {
                    var parsedResult = ParseResultStringInto<T>(readResultTask.Result);
                    getResult = () => { return parsedResult; };
                    successCallback.InvokeIfNotNull(parsedResult);
                }
            };
            return sendTask.ContinueWith<T>((_) => {
                readResultTask.Wait();
                return getResult();
            });
        }

        private T ParseResultStringInto<T>(string result) { return jsonReader.Read<T>(result); }

        public RestRequest Send(HttpMethod method) {
            sendTask = new Task(() => {
                using (var c = new HttpClient()) {
                    using (var asyncRestRequest = c.SendAsync(new HttpRequestMessage(method, uri))) {
                        asyncRestRequest.Wait(); //helps so that other thread can set handleResult in time
                        handleResult.InvokeIfNotNull(this, asyncRestRequest.Result); // calling resp.Result blocks the thread
                    }
                }
            });
            sendTask.Start();
            return this;
        }

        internal RestRequest Send(object get) {
            throw new NotImplementedException();
        }
    }
}