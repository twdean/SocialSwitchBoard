using LinqToTwitter;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Net;
using System.IO;
using System.Text;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Testing.Models;
using agsXMPP;
using agsXMPP.protocol.client;
using agsXMPP.protocol.iq.roster;
using agsXMPP.Collections;
using Newtonsoft.Json;
using Facebook;

namespace Testing.Controllers
{
    public class HomeController : Controller
    {
        XmppClientConnection xmpp = new XmppClientConnection("chat.facebook.com");
        public string MessageContent { get; set; }

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

            if(Session["FacebookContext"] != null)
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

                if(Session["TwitterContext"] != null)
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
            var tUser =
                    (from user in twitterCtx.User
                     where user.Type == UserType.Lookup &&
                           user.ScreenName == "l9digital"
                     select user).FirstOrDefault();

            if (!String.IsNullOrWhiteSpace(MessageContent))
            {
                twitterCtx.UpdateStatus(MessageContent);
                twitterCtx.NewDirectMessage(tUser.Identifier.UserID, MessageContent);
            }

        }

        public void SendFacebookMessage()
        {
            xmpp.Open("therealtrevordean", "export1313");
            xmpp.OnLogin += new ObjectHandler(OnLogin);
        }

        private string GetFacebookFriends(string friendName)
        {
            const int batchSize = 50;
            string facebookId = string.Empty;
            var parameters = new Dictionary<string, object>();

            var accessToken = (String)Session["FacebookContext"];

            var client = new FacebookClient(accessToken);

            for (long q = 0; q < 5000; q += batchSize)
            {
                parameters["limit"] = batchSize;
                parameters["offset"] = q;

                dynamic myFriends = client.Get("me/friends", parameters);
                foreach (dynamic friend in myFriends.data)
                {
                    if (friend.name == friendName)
                    {
                        facebookId = friend.id;
                    }
                }
            }

            return facebookId;
        }

        private void OnLogin(object sender)
        {
            //string recieverId = "-721040555@chat.facebook.com"; //Trevor
            string recieverId = "-718145001@chat.facebook.com"; //Karim 

            xmpp.Send(new agsXMPP.protocol.client.Message(new Jid(recieverId), agsXMPP.protocol.client.MessageType.chat, MessageContent));
        }

        [HttpPost]
        public ActionResult SendMessage(string message)
        {
            MessageContent = message;

            SendTwitterMessage();
            SendFacebookMessage();

            return View("Index");
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
