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

    }
}