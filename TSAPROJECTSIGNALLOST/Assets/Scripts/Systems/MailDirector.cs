using System.Collections.Generic;
using UnityEngine;

namespace SignalLost
{
    /// <summary>
    /// The social puzzle, now an end-of-day ritual. Each night one story message arrives (by day number),
    /// and every other night a side message. Every reply is a trade-off: it gives something and costs
    /// something (tomorrow's Data, shift length, Gold Static, events, or trust with another sender).
    /// Trust (hale, iva, static) stays hidden and decides the ending. Extra replies need Open Channel.
    /// </summary>
    public class MailDirector : MonoBehaviour
    {
        public static MailDirector I { get; private set; }

        public readonly List<MailEntry> Inbox = new List<MailEntry>();

        private readonly Dictionary<string, int> trust = new Dictionary<string, int>();
        private readonly List<string> flavorSeen = new List<string>();
        private readonly List<FollowupSave> pending = new List<FollowupSave>();
        private float flavorTimer = 120f;
        private bool ready;

        private void Awake()
        {
            I = this;
            trust["hale"] = 0;
            trust["iva"] = 0;
            trust["static"] = 0;
        }

        private void OnEnable()
        {
            EventBus.GameReady += OnReady;
            EventBus.DayEnded += OnDayEnded;
        }

        private void OnDisable()
        {
            EventBus.GameReady -= OnReady;
            EventBus.DayEnded -= OnDayEnded;
        }

        /// <summary>Replies still waiting on the end-of-day screen.</summary>
        public bool AnyNeedsReply
        {
            get
            {
                foreach (MailEntry e in Inbox) if (e.NeedsReply) return true;
                return false;
            }
        }

        /// <summary>Title-card line for a day (mail.json briefings).</summary>
        public DayBriefing BriefingFor(int day)
        {
            DayBriefing best = null;
            foreach (DayBriefing b in Balance.I.Mail.briefings)
            {
                if (b.day == day) return b;
                if (b.day < day && (best == null || b.day > best.day)) best = b;
            }
            return best;
        }

        /// <summary>Night mail: tonight's story message, and a side message every other night.</summary>
        private void OnDayEnded(int day)
        {
            EnsureStoryMail(false);
            if (day % 2 == 0 && !HasUnreadFlavor())
            {
                MailDef next = PickFlavor();
                if (next != null)
                {
                    flavorSeen.Add(next.id);
                    Deliver(next, false);
                }
            }
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                DeliverFollowup(pending[i].from, pending[i].text);
                pending.RemoveAt(i);
            }
        }

        public int UnreadCount
        {
            get
            {
                int n = 0;
                foreach (MailEntry e in Inbox) if (!e.Read || e.NeedsReply) n++;
                return n;
            }
        }

        public int Trust(string who)
        {
            return trust.TryGetValue(who, out int v) ? v : 0;
        }

        private void OnReady()
        {
            ready = true;
        }

        private bool HasMail(string id)
        {
            foreach (MailEntry e in Inbox) if (e.MailId == id && !e.IsFollowup) return true;
            return false;
        }

        /// <summary>Story mail: by day number, delivered on that day's end-of-day screen, in order.</summary>
        public void EnsureStoryMail(bool silent)
        {
            int day = DayManager.I != null ? DayManager.I.Day : 1;
            List<MailDef> story = new List<MailDef>();
            foreach (MailDef m in Balance.I.Mail.messages)
            {
                if (m.trigger == null || m.trigger.type != "day") continue;
                if (m.trigger.level <= day && !HasMail(m.id)) story.Add(m);
            }
            story.Sort((a, b) => a.trigger.level.CompareTo(b.trigger.level));
            foreach (MailDef m in story) Deliver(m, silent);
        }

        /// <summary>Antenna Lv 10: ??? writes. Replying triggers the ending.</summary>
        public void EnsureFinaleMail()
        {
            foreach (MailDef m in Balance.I.Mail.messages)
            {
                if (m.trigger == null || m.trigger.type != "antenna" || m.trigger.level < Balance.B.endingAntennaLevel) continue;
                foreach (MailEntry e in Inbox)
                {
                    if (e.MailId == m.id && !e.IsFollowup)
                    {
                        e.Replied = -1;
                        e.Read = false;
                        Inbox.Remove(e);
                        Inbox.Insert(0, e);
                        EventBus.RaiseInboxChanged();
                        return;
                    }
                }
                Deliver(m, false);
                return;
            }
        }

        public MailEntry Deliver(MailDef def, bool silent)
        {
            MailEntry e = new MailEntry { MailId = def.id, Def = def, From = def.from };
            Inbox.Insert(0, e);
            if (!silent) AudioManager.Play("mail");
            EventBus.RaiseMailReceived(e);
            EventBus.RaiseInboxChanged();
            return e;
        }

        private void DeliverFollowup(string from, string text)
        {
            MailEntry e = new MailEntry { MailId = "followup", From = from, FollowupText = text };
            Inbox.Insert(0, e);
            AudioManager.Play("mail", 1.15f, 0.7f);
            EventBus.RaiseMailReceived(e);
            EventBus.RaiseInboxChanged();
        }

        private void Update()
        {
            if (!ready || GameManager.I == null) return;
            // Follow-ups to tonight's replies arrive a moment later on the same end-of-day screen.
            bool night = DayManager.I != null && DayManager.I.Phase == DayPhase.Night;
            if (!night && GameManager.I.State != GameState.Finale) return;
            float dt = Time.unscaledDeltaTime;
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                pending[i].remaining -= dt;
                if (pending[i].remaining <= 0f)
                {
                    FollowupSave f = pending[i];
                    pending.RemoveAt(i);
                    DeliverFollowup(f.from, f.text);
                }
            }
        }

        private bool HasUnreadFlavor()
        {
            foreach (MailEntry e in Inbox)
            {
                if (e.Def != null && e.Def.trigger != null && e.Def.trigger.type == "flavor" && (!e.Read || e.NeedsReply)) return true;
            }
            return false;
        }

        private MailDef PickFlavor()
        {
            List<MailDef> options = new List<MailDef>();
            foreach (MailDef m in Balance.I.Mail.messages)
            {
                if (m.trigger == null || m.trigger.type != "flavor") continue;
                if (m.trigger.minAntenna > GameStats.Antenna) continue;
                if (m.replies == null || m.replies.Count == 0) continue;
                if (flavorSeen.Contains(m.id)) continue;
                options.Add(m);
            }
            if (options.Count == 0)
            {
                // Pool exhausted: start over.
                flavorSeen.Clear();
                foreach (MailDef m in Balance.I.Mail.messages)
                {
                    if (m.trigger != null && m.trigger.type == "flavor" && m.trigger.minAntenna <= GameStats.Antenna) options.Add(m);
                }
            }
            return options.Count > 0 ? options[Random.Range(0, options.Count)] : null;
        }

        public void MarkRead(MailEntry e)
        {
            if (e == null || e.Read) return;
            e.Read = true;
            EventBus.RaiseInboxChanged();
        }

        /// <summary>Replies the player can see right now (extra replies need Open Channel).</summary>
        public List<int> VisibleReplyIndices(MailDef def)
        {
            List<int> list = new List<int>();
            if (def == null || def.replies == null) return list;
            for (int i = 0; i < def.replies.Count; i++)
            {
                if (!def.replies[i].NeedsOpenChannel || GameStats.OpenChannel) list.Add(i);
            }
            return list;
        }

        public void Reply(MailEntry e, int replyIndex)
        {
            if (e == null || e.Def == null || !e.NeedsReply) return;
            if (replyIndex < 0 || replyIndex >= e.Def.replies.Count) return;
            MailReply r = e.Def.replies[replyIndex];
            if (r.NeedsOpenChannel && !GameStats.OpenChannel) return;
            e.Replied = replyIndex;
            e.Read = true;

            if (r.trust != null)
            {
                foreach (KeyValuePair<string, int> kv in r.trust) trust[kv.Key] = Trust(kv.Key) + kv.Value;
                EventBus.RaiseTrustChanged();
            }
            if (!string.IsNullOrEmpty(r.followup))
            {
                pending.Add(new FollowupSave { from = e.From, text = r.followup, remaining = 1.2f });
            }
            bool ending = false;
            if (r.effects != null)
            {
                foreach (MailEffect fx in r.effects)
                {
                    if (fx.instant == "ending") ending = true;
                    else ApplyEffect(fx, e);
                }
            }
            AudioManager.Play("buy", 1.2f, 0.6f);
            EventBus.RaiseInboxChanged();
            SaveManager.I.RequestSave();
            if (ending) GameManager.I.TriggerEnding();
        }

        private void ApplyEffect(MailEffect fx, MailEntry e)
        {
            if (!string.IsNullOrEmpty(fx.buff))
            {
                // Night replies: timed buffs wait and run during tomorrow's shift.
                if (fx.buff == "golden_hour")
                {
                    if (DayManager.I != null) DayManager.I.AddTomorrow("golden_mult", 1d);
                    return;
                }
                BuffSystem.I.Add(fx.buff, fx.amount, fx.seconds, BuffSystem.Describe(fx.buff, fx.amount));
                return;
            }
            switch (fx.instant)
            {
                case "tomorrow":
                    if (DayManager.I != null && !string.IsNullOrEmpty(fx.value)) DayManager.I.AddTomorrow(fx.value, fx.amount);
                    break;
                case "data_days":
                    {
                        double today = DayManager.I != null ? DayManager.I.Report.dataEarned : 0d;
                        double amount = System.Math.Max(10d, today * fx.amount);
                        GameManager.I.Earn(amount, BlobSpawner.I.ViewCenter + Vector3.up, 0);
                        if (HUD.I != null) HUD.I.Toast("+" + NumberFormat.Format(amount) + " DATA", Palette.Gold);
                        break;
                    }
                case "drone_skin":
                    GameManager.I.SetDroneSkin(fx.value);
                    if (HUD.I != null) HUD.I.Toast("NEW DRONE SKIN: " + (fx.value ?? "").ToUpperInvariant(), Palette.Pink);
                    break;
                case "golden_hour":
                    if (DayManager.I != null) DayManager.I.AddTomorrow("golden_mult", 1d);
                    break;
            }
        }

        public void ReadSave(SaveData s)
        {
            Inbox.Clear();
            trust["hale"] = 0;
            trust["iva"] = 0;
            trust["static"] = 0;
            if (s.trust != null) foreach (KeyValuePair<string, int> kv in s.trust) trust[kv.Key] = kv.Value;
            flavorSeen.Clear();
            pending.Clear();
            if (s.mail == null) return;
            if (s.mail.flavorSeen != null) flavorSeen.AddRange(s.mail.flavorSeen);
            if (s.mail.pending != null) pending.AddRange(s.mail.pending);
            if (s.mail.inbox != null)
            {
                foreach (InboxSave i in s.mail.inbox)
                {
                    MailDef def = i.mailId == "followup" ? null : Balance.I.MailById(i.mailId);
                    if (def == null && i.mailId != "followup") continue;
                    Inbox.Add(new MailEntry { MailId = i.mailId, Def = def, From = i.from, FollowupText = i.followupText, Read = i.read, Replied = i.replied });
                }
            }
        }

        public void WriteSave(SaveData s)
        {
            s.trust = new Dictionary<string, int>(trust);
            MailSave m = new MailSave();
            foreach (MailEntry e in Inbox)
            {
                m.inbox.Add(new InboxSave { mailId = e.MailId, from = e.From, followupText = e.FollowupText, read = e.Read, replied = e.Replied });
                if (e.IsFollowup) continue;
                if (e.Read) m.read.Add(e.MailId);
                else m.unread.Add(e.MailId);
            }
            m.flavorSeen = new List<string>(flavorSeen);
            m.pending = new List<FollowupSave>(pending);
            s.mail = m;
        }
    }
}
