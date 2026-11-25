using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace SignalLost
{
    /// <summary>
    /// Loads every data file from StreamingAssets/Data. All tunable numbers live in balance.json;
    /// the skill tree, events, mail, tutorial and cutscenes are their own JSON files.
    /// </summary>
    public class Balance : MonoBehaviour
    {
        public static Balance I { get; private set; }

        /// <summary>Shortcut for Balance.I.Data.</summary>
        public static BalanceData B { get { return I != null ? I.Data : null; } }

        public BalanceData Data { get; private set; }
        /// <summary>Every node in tree.json (main tree and Deep Signal tree).</summary>
        public List<UpgradeDef> Upgrades { get; private set; }
        public CutsceneFile Cutscenes { get; private set; }
        public List<EventDef> Events { get; private set; }
        public MailFile Mail { get; private set; }
        public TutorialFile Tutorial { get; private set; }
        public bool Loaded { get; private set; }

        private readonly Dictionary<string, UpgradeDef> upgradeById = new Dictionary<string, UpgradeDef>();
        private readonly Dictionary<string, MailDef> mailById = new Dictionary<string, MailDef>();
        private readonly Dictionary<string, SenderDef> senderById = new Dictionary<string, SenderDef>();

        private void Awake()
        {
            I = this;
        }

        public IEnumerator LoadAll()
        {
            string balanceJson = null, upgradesJson = null, cutscenesJson = null, eventsJson = null, mailJson = null, tutorialJson = null;
            yield return ReadText("balance.json", s => balanceJson = s);
            yield return ReadText("tree.json", s => upgradesJson = s);
            yield return ReadText("cutscenes.json", s => cutscenesJson = s);
            yield return ReadText("events.json", s => eventsJson = s);
            yield return ReadText("mail.json", s => mailJson = s);
            yield return ReadText("tutorial.json", s => tutorialJson = s);

            Data = SafeParse(balanceJson, new BalanceData());
            Upgrades = SafeParse(upgradesJson, new List<UpgradeDef>());
            Cutscenes = SafeParse(cutscenesJson, new CutsceneFile());
            Events = SafeParse(eventsJson, new List<EventDef>());
            Mail = SafeParse(mailJson, new MailFile());
            Tutorial = SafeParse(tutorialJson, new TutorialFile());

            upgradeById.Clear();
            foreach (UpgradeDef u in Upgrades) upgradeById[u.id] = u;
            mailById.Clear();
            foreach (MailDef m in Mail.messages) mailById[m.id] = m;
            senderById.Clear();
            foreach (SenderDef s in Mail.senders) senderById[s.id] = s;

            Loaded = true;
        }

        public UpgradeDef Upgrade(string id)
        {
            return id != null && upgradeById.TryGetValue(id, out UpgradeDef u) ? u : null;
        }

        public CutsceneDef Cutscene(string id)
        {
            if (Cutscenes == null || id == null) return null;
            foreach (CutsceneDef c in Cutscenes.cutscenes) if (c.id == id) return c;
            return null;
        }

        public MailDef MailById(string id)
        {
            return id != null && mailById.TryGetValue(id, out MailDef m) ? m : null;
        }

        public SenderDef Sender(string id)
        {
            if (id != null && senderById.TryGetValue(id, out SenderDef s)) return s;
            return new SenderDef { id = id, name = id, portrait = id };
        }

        public BlobTypeDef BlobType(string id)
        {
            foreach (BlobTypeDef t in Data.blobs) if (t.id == id) return t;
            return null;
        }

        private static T SafeParse<T>(string json, T fallback)
        {
            if (string.IsNullOrEmpty(json)) return fallback;
            try
            {
                T result = JsonUtil.Parse<T>(json);
                return result != null ? result : fallback;
            }
            catch (Exception e)
            {
                Debug.LogError("[Balance] Could not parse data file: " + e.Message);
                return fallback;
            }
        }

        /// <summary>File.ReadAllText on desktop; UnityWebRequest on WebGL/Android where StreamingAssets is not a folder.</summary>
        public static IEnumerator ReadText(string fileName, Action<string> done)
        {
            string path = Path.Combine(Application.streamingAssetsPath, "Data", fileName);
#if (UNITY_WEBGL || UNITY_ANDROID) && !UNITY_EDITOR
            using (UnityWebRequest req = UnityWebRequest.Get(path))
            {
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success) done(req.downloadHandler.text);
                else { Debug.LogError("[Balance] Missing data file " + path + ": " + req.error); done(null); }
            }
#else
            if (File.Exists(path))
            {
                done(File.ReadAllText(path));
            }
            else
            {
                Debug.LogError("[Balance] Missing data file " + path);
                done(null);
            }
            yield break;
#endif
        }
    }
}
