using System.Reflection;
using LinqToTwitter;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web.Mvc;
using Matrix.Xmpp;
using Matrix.Xmpp.Client;
using Matrix.Xmpp.Sasl;
using Matrix.Xmpp.Sasl.Processor.Facebook;
using Matrix.Xmpp.XHtmlIM;
using Testing.Models;
using Facebook;
using Jid = Matrix.Jid;

namespace Testing.Controllers
{
    public class HomeController : Controller
    {
        public XmppClient xmpp = new XmppClient();


        //XmppClientConnection xmpp = new XmppClientConnection("chat.facebook.com");
        public string MessageContent { get; set; }
        public string FacebookId { get; set; }
        public string TwitterId { get; set; }
        public List<string> FacebookFriendIds { get; set; }

        public ActionResult Login()
        {
            return View();
        }

        public ActionResult AuthenticateFacebook()
        {

            var facebookKey = ConfigurationManager.AppSettings["FacebookKey"];
            var facebookSecret = ConfigurationManager.AppSettings["FacebookSecret"];


            var uriBuilder = new UriBuilder(Request.Url)
            {
                Query = null,
                Fragment = null,
                Path = Url.Action("FacebookCallback")
            }.Uri;


            var fb = new FacebookClient();
            var loginUrl = fb.GetLoginUrl(new
            {
                client_id = facebookKey,
                client_secret = facebookSecret,
                redirect_uri = uriBuilder.AbsoluteUri,
                response_type = "code",
                scope = "email,user_status, user_photos, read_stream, read_insights, user_online_presence, xmpp_login"
            });

            return Redirect(loginUrl.AbsoluteUri);
        }

        public ActionResult FacebookCallback(string code)
        {
            var facebookKey = ConfigurationManager.AppSettings["FacebookKey"];
            var facebookSecret = ConfigurationManager.AppSettings["FacebookSecret"];

            var uriBuilder = new UriBuilder(Request.Url)
            {
                Query = null,
                Fragment = null,
                Path = Url.Action("FacebookCallback")
            }.Uri;


            var fb = new FacebookClient();
            dynamic result = fb.Post("oauth/access_token", new
            {
                client_id = facebookKey,
                client_secret = facebookSecret,
                redirect_uri = uriBuilder.AbsoluteUri,
                code = code
            });

            var accessToken = result.access_token;

            if (Session["FacebookContext"] != null)
            {
                Session["FacebookContext"] = accessToken;
            }
            else
            {
                Session.Add("FacebookContext", accessToken);
            }

            return RedirectToAction("Login", "Home");
        }

        [AllowAnonymous]
        public ActionResult AuthenticateTwitter()
        {
            MvcAuthorizer auth = GetAuthorizer(null);
            var twitterReturnUrl = ConfigurationManager.AppSettings["TwitterAutorizationReturnUrl"];


            if (!auth.CompleteAuthorization(Request.Url))
            {
                var specialUri = new System.Uri(twitterReturnUrl);
                ActionResult res = auth.BeginAuthorization(specialUri);
                return res;
            }

            return TwitterAppAuthorizationConfirmation(null);
        }

        private MvcAuthorizer GetAuthorizer(string twitterUserId)
        {
            var twitterKey = ConfigurationManager.AppSettings["TwitterConsumerKey"];
            var twitterSecret = ConfigurationManager.AppSettings["TwitterConsumerSecret"];

            IOAuthCredentials credentials = new InMemoryCredentials();

            if (credentials.ConsumerKey == null || credentials.ConsumerSecret == null)
            {
                credentials.ConsumerKey = twitterKey;
                credentials.ConsumerSecret = twitterSecret;
            }

            if (!string.IsNullOrEmpty(twitterUserId))
            {
                credentials.AccessToken = "";
                credentials.OAuthToken = "";
            }


            var auth = new MvcAuthorizer
            {
                Credentials = credentials
            };

            return auth;
        }


        public ActionResult TwitterAppAuthorizationConfirmation(string returnUrl)
        {
            try
            {
                var auth = GetAuthorizer(null);

                auth.CompleteAuthorization(Request.Url);

                if (!auth.IsAuthorized)
                {
                    var specialUri = new System.Uri("/Home/AppAuthorizationConfirmation");
                    return auth.BeginAuthorization(specialUri);
                }

                var twitterCtx = new TwitterContext(auth);

                if (Session["TwitterContext"] != null)
                {
                    Session["TwitterContext"] = twitterCtx;
                }
                else
                {
                    Session.Add("TwitterContext", twitterCtx);
                }

            }
            catch (TwitterQueryException tqEx)
            {
                return View("Error");
            }


            return RedirectToAction("Login", "Home");
        }

        [HttpGet]
        public ActionResult Index()
        {
            return View();
        }


        public void SendTwitterMessage()
        {
            var twitterCtx = (TwitterContext)Session["TwitterContext"];
            var tUser1 =
                    (from user in twitterCtx.User
                     where user.Type == UserType.Lookup &&
                           user.ScreenName == "karimawad"
                     select user).FirstOrDefault();

            var tUser2 =
                    (from user in twitterCtx.User
                     where user.Type == UserType.Lookup &&
                           user.ScreenName == "kmore"
                     select user).FirstOrDefault();


            if (!String.IsNullOrWhiteSpace(MessageContent))
            {
                twitterCtx.UpdateStatus(MessageContent);
                twitterCtx.NewDirectMessage(tUser1.Identifier.UserID, MessageContent);
                twitterCtx.NewDirectMessage(tUser2.Identifier.UserID, MessageContent);
            }

        }

        public void SendFacebookMessage()
        {
            xmpp.OnBeforeSasl += xmpp_OnBeforeSasl;
            xmpp.OnError += xmpp_OnError;
            xmpp.XmppDomain = "chat.facebook.com";
            xmpp.Port = 5222;
            xmpp.Open();

            FacebookFriendIds = GetFacebookFriends(new List<string> { "Karim Awad", "Kerry Morrison" });

            string senderId = string.Format("-{0}@chat.facebook.com", FacebookId);

            foreach (string id in FacebookFriendIds)
            {
                string recieverId = string.Format("-{0}@chat.facebook.com", id);

                var msg = new Message
                {
                    Type = MessageType.chat,
                    To = new Jid(recieverId),
                    From = new Jid(senderId),
                    Body = MessageContent
                };

                xmpp.Send(msg);
            }

            xmpp.Close();
        }

        void xmpp_OnError(object sender, Matrix.ExceptionEventArgs e)
        {
            var message = e.Exception.ToString();
        }

        void xmpp_OnBeforeSasl(object sender, Matrix.Xmpp.Sasl.SaslEventArgs e)
        {
            var facebookKey = ConfigurationManager.AppSettings["FacebookKey"];
            var facebookSecret = ConfigurationManager.AppSettings["FacebookSecret"];

            e.Auto = false;
            e.SaslMechanism = SaslMechanism.X_FACEBOOK_PLATFORM;
            e.SaslProperties = new FacebookProperties
            {
                ApiKey = facebookKey,
                ApiSecret = facebookSecret,
                AccessToken = (String)Session["FacebookContext"]

            };
        }


        private List<string> GetFacebookFriends(List<string> friendNames)
        {
            const int batchSize = 50;
            List<string> facebookIDs = new List<string>();
            var parameters = new Dictionary<string, object>();

            var accessToken = (String)Session["FacebookContext"];

            var client = new FacebookClient(accessToken);
            dynamic me = client.Get("me");
            FacebookId = me.id;

            for (long q = 0; q < 5000; q += batchSize)
            {
                parameters["limit"] = batchSize;
                parameters["offset"] = q;

                dynamic myFriends = client.Get("me/friends", parameters);

                foreach (dynamic friend in myFriends.data)
                {
                    if (friendNames.Contains(friend.name))
                    {
                        facebookIDs.Add(friend.id);
                    }
                }
            }

            return facebookIDs;
        }

        [HttpPost]
        public ActionResult SendMessage(MessageModel message)
        {
            try
            {
                MessageContent = message.MessageContent;

                ModelState.Clear();

                //SendTwitterMessage();
                SendFacebookMessage();

                message.MessageContent = "";
                message.StatusMessage = "Message sent successfully!";
            }
            catch (Exception ex)
            {
                message.StatusMessage = "Error Sending Message";
            }


            return View("Index", message);
        }

        [HttpPost]
        public ActionResult Create(FormCollection collection)
        {
            try
            {
                // TODO: Add insert logic here

                return RedirectToAction("Index");
            }
            catch
            {
                return View();
            }
        }

        //
        // GET: /Admin/Edit/5

        public ActionResult Edit(int id)
        {
            return View();
        }

        //
        // POST: /Admin/Edit/5

        [HttpPost]
        public ActionResult Edit(int id, FormCollection collection)
        {
            try
            {
                // TODO: Add update logic here

                return RedirectToAction("Index");
            }
            catch
            {
                return View();
            }
        }

        //
        // GET: /Admin/Delete/5

        public ActionResult Delete(int id)
        {
            return View();
        }

        //
        // POST: /Admin/Delete/5

        [HttpPost]
        public ActionResult Delete(int id, FormCollection collection)
        {
            try
            {
                // TODO: Add delete logic here

                return RedirectToAction("Index");
            }
            catch
            {
                return View();
            }
        }
    }
}
