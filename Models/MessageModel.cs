using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Testing.Models
{
    public class MessageModel
    {
        public string MessageContent { get; set; }
        public List<RecipientModel> Recipient { get; set; }
        public string StatusMessage { get; set; }
        public string TwitterAuthToken { get; set; }
        public string FacebookAuthToken { get; set; }
        public string CurrentFacebookId { get; set; }
        public string CurrentTwitterId { get; set; }
    }
}