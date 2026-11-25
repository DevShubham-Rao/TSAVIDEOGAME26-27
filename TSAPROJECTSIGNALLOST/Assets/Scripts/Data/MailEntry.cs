namespace SignalLost
{
    /// <summary>One message in the inbox: a story/flavor mail, or a one-line follow-up from a sender.</summary>
    public class MailEntry
    {
        public string MailId;
        public MailDef Def;
        public string From;
        public string FollowupText;
        public bool Read;
        public int Replied = -1;

        public bool IsFollowup { get { return Def == null; } }
        public bool NeedsReply { get { return Def != null && Def.replies != null && Def.replies.Count > 0 && Replied < 0; } }
        public bool IsStory { get { return Def != null && Def.trigger != null && Def.trigger.type == "antenna"; } }

        public string Subject
        {
            get
            {
                if (Def != null) return Def.subject;
                return "Re: your reply";
            }
        }
    }
}
