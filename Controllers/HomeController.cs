using agsXMPP.Sasl.Facebook;
using LinqToTwitter;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web.Mvc;
using Testing.Models;
using Facebook;
using agsXMPP;

using log4net;


namespace Testing.Controllers
{
    public class HomeController : Controller
    {
        public string MessageContent { get; set; }
        public string FacebookId { get; set; }
        public string TwitterId { get; set; }
        public List<string> FacebookFriendIds { get; set; }
        private static readonly ILog log = LogManager.GetLogger(typeof(HomeController));
        public string FacebookAccessToken  { get; set; }   
        private XmppClientConnection xmppClient = new XmppClientConnection();

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

        public void SendFacebookSasl()
        {
            log.Debug("Starting xmpp stack");

            xmppClient.Server = "chat.facebook.com";
            xmppClient.Port = 5222;
            FacebookAccessToken = (String) (Session["FacebookContext"]);
            xmppClient.OnSaslStart += (sender, args) =>
            {
                log.Debug("xmppClient_OnSaslStart");

                log.Debug("AccessToken: " + FacebookAccessToken);
                var facebookKey = ConfigurationManager.AppSettings["FacebookKey"];


                args.Auto = false;
                args.Mechanism = agsXMPP.protocol.sasl.Mechanism.GetMechanismName(agsXMPP.protocol.sasl.MechanismType.X_FACEBOOK_PLATFORM);
                args.ExtentedData = new FacebookExtendedData
                {

                    ApiKey = facebookKey,
                    AccessToken = FacebookAccessToken
                };
            };

            xmppClient.OnLogin += (sender) =>
            {
                log.Debug("xmppClient_OnLogin");

                FacebookFriendIds = GetFacebookFriends(new List<string> { "Karim Awad", "Kerry Morrison" });

                var senderId = string.Format("-{0}@chat.facebook.com", FacebookId);

                foreach (string id in FacebookFriendIds)
                {
                    var recieverId = string.Format("-{0}@chat.facebook.com", id);
                    xmppClient.Send(new agsXMPP.protocol.client.Message(new agsXMPP.Jid(recieverId), new agsXMPP.Jid(senderId), agsXMPP.protocol.client.MessageType.chat, MessageContent));
                }
            };

            xmppClient.OnError += xmppClient_OnError;
            xmppClient.Open();
            log.Debug("Finished xmpp stack");
        }

        void xmppClient_OnError(object sender, Exception ex)
        {
            log.Debug(string.Format("Error: {0}", ex.ToString()));
        }

        private List<string> GetFacebookFriends(List<string> friendNames)
        {
            const int batchSize = 50;
            var facebookIDs = new List<string>();
            var parameters = new Dictionary<string, object>();

            var client = new FacebookClient(FacebookAccessToken);
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

                SendTwitterMessage();
                SendFacebookSasl();

                message.MessageContent = "";
                message.StatusMessage = "Message sent successfully!";
            }
            catch (Exception ex)
            {
                message.StatusMessage = "Error Sending Message";
            }


            return View("Index", message);
        }

    }
}
