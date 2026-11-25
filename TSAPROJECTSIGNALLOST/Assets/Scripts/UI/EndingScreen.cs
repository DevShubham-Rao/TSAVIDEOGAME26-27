using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SignalLost
{
    /// <summary>
    /// Each ending plays a world moment, then 3 to 5 full-screen text cards over the world, then a
    /// "Thanks for playing" card with stats and a Rebroadcast button.
    /// </summary>
    public class EndingScreen : MonoBehaviour
    {
        public static EndingScreen I { get; private set; }

        private CanvasGroup group;
        private Image backdrop;
        private TextMeshProUGUI cardText;
        private RectTransform cardSymbols;
        private RectTransform cardHolder;
        private TextMeshProUGUI hint;
        private CanvasGroup finalGroup;
        private TextMeshProUGUI finalTitle;
        private TextMeshProUGUI finalEnding;
        private TextMeshProUGUI finalCount;
        private TextMeshProUGUI finalMessage;
        private TextMeshProUGUI finalStats;
        private TextMeshProUGUI rebroadcastLabel;
        private bool advance;
        private bool fireworks;
        private Material skyInstance;

        private void Awake()
        {
            I = this;
            group = UIFactory.Group(gameObject);
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
            Build();
        }

        private void Build()
        {
            RectTransform root = (RectTransform)transform;
            backdrop = UIFactory.Image(root, "Backdrop", UISprites.Square, new Color(0.01f, 0.02f, 0.06f, 0f), true);
            UIFactory.Stretch(backdrop.rectTransform);
            backdrop.gameObject.AddComponent<Button>().onClick.AddListener(() => advance = true);

            cardHolder = UIFactory.Place(UIFactory.Rect("Card", root), UIFactory.Center, UIFactory.Center, new Vector2(0f, 40f), new Vector2(1500f, 500f));
            cardText = UIFactory.GlowText(cardHolder, "Text", "", 58f, Color.white);
            UIFactory.Stretch(cardText.rectTransform);
            hint = UIFactory.Text(root, "Hint", "CLICK TO CONTINUE", 24f, Palette.TextSoft);
            UIFactory.Place(hint.rectTransform, UIFactory.BottomCenter, UIFactory.BottomCenter, new Vector2(0f, 60f), new Vector2(800f, 40f));

            Image fp = UIFactory.Panel(root, "Final");
            UIFactory.Place(fp.rectTransform, UIFactory.Center, UIFactory.Center, Vector2.zero, new Vector2(1100f, 760f));
            finalGroup = UIFactory.Group(fp.gameObject);
            finalTitle = UIFactory.GlowText(fp.transform, "Title", "THANKS FOR PLAYING", 72f, Palette.Cyan);
            UIFactory.Place(finalTitle.rectTransform, UIFactory.TopCenter, UIFactory.TopCenter, new Vector2(0f, -30f), new Vector2(1000f, 90f));
            finalCount = UIFactory.GlowText(fp.transform, "EndingCount", "", 54f, Palette.Gold);
            UIFactory.Place(finalCount.rectTransform, UIFactory.TopCenter, UIFactory.TopCenter, new Vector2(0f, -118f), new Vector2(1000f, 66f));
            finalEnding = UIFactory.Text(fp.transform, "Ending", "", 38f, Palette.Magenta);
            UIFactory.Place(finalEnding.rectTransform, UIFactory.TopCenter, UIFactory.TopCenter, new Vector2(0f, -188f), new Vector2(1000f, 52f));
            finalStats = UIFactory.Text(fp.transform, "Stats", "", 30f, Color.white, TextAlignmentOptions.Center, FontStyles.Normal);
            UIFactory.Place(finalStats.rectTransform, UIFactory.TopCenter, UIFactory.TopCenter, new Vector2(0f, -250f), new Vector2(1000f, 230f));
            Button rebroadcast = UIFactory.Button(fp.transform, "Rebroadcast", "REBROADCAST", 32f, OnRebroadcast, new Color(0.2f, 0.05f, 0.22f, 1f), Palette.Magenta);
            UIFactory.Place((RectTransform)rebroadcast.transform, UIFactory.BottomCenter, UIFactory.BottomCenter, new Vector2(-250f, 50f), new Vector2(460f, 110f));
            rebroadcastLabel = rebroadcast.GetComponentInChildren<TextMeshProUGUI>();
            Button keep = UIFactory.Button(fp.transform, "Keep", "KEEP LISTENING", 32f, OnKeepPlaying);
            UIFactory.Place((RectTransform)keep.transform, UIFactory.BottomCenter, UIFactory.BottomCenter, new Vector2(250f, 50f), new Vector2(460f, 110f));
            finalMessage = UIFactory.Text(fp.transform, "Note", "", 26f, Palette.Gold, TextAlignmentOptions.Center, FontStyles.Normal);
            UIFactory.Place(finalMessage.rectTransform, UIFactory.BottomCenter, UIFactory.BottomCenter, new Vector2(0f, 176f), new Vector2(1020f, 96f));
            finalMessage.enableWordWrapping = true;
            finalGroup.alpha = 0f;
            finalGroup.blocksRaycasts = false;
            finalGroup.interactable = false;
        }

        public void Play(EndingDef def)
        {
            if (def == null)
            {
                def = new EndingDef { id = GameManager.I.EndingId, name = "THE END", cards = { "The signal is clear." } };
            }
            if (MailUI.I != null) MailUI.I.Close();
            if (EndOfDayScreen.I != null && EndOfDayScreen.I.IsOpen) EndOfDayScreen.I.Close();
            if (SettingsScreen.I != null) SettingsScreen.I.Close();
            if (PauseMenu.I != null) PauseMenu.I.Close();
            StopAllCoroutines();
            StartCoroutine(Sequence(def));
        }

        private IEnumerator Sequence(EndingDef def)
        {
            group.alpha = 1f;
            group.blocksRaycasts = true;
            group.interactable = true;
            cardText.text = "";
            hint.gameObject.SetActive(false);
            finalGroup.alpha = 0f;
            finalGroup.blocksRaycasts = false;

            yield return WorldMoment(def.id);

            float t = 0f;
            while (t < 0.8f)
            {
                t += Time.unscaledDeltaTime;
                backdrop.color = new Color(0.01f, 0.02f, 0.06f, Mathf.Lerp(0f, 0.86f, t / 0.8f));
                yield return null;
            }

            Color tint = Palette.Hex(def.tint);
            foreach (string card in def.cards)
            {
                yield return ShowCard(card, tint);
            }
            fireworks = false;
            cardText.text = "";
            if (cardSymbols != null) Destroy(cardSymbols.gameObject);
            hint.gameObject.SetActive(false);

            StatsTracking s = GameManager.I.Tracking;
            int seen = GameManager.I.EndingsSeen.Count;
            int total = Mathf.Max(3, Balance.I.Mail.endings.Count);
            finalCount.text = "ENDING " + seen + "/" + total;
            finalEnding.text = def.name.ToUpperInvariant();
            finalEnding.color = tint;
            int left = total - seen;
            int echoesNow = GameManager.I.Echoes;
            if (left > 0)
            {
                finalMessage.text = (GameManager.I.LastEndingWasNew ? "You got Ending " + seen + "/" + total + "!" : "You have found " + seen + " of " + total + " endings.")
                                    + " Spend your " + echoesNow + " Echoes in the upgrade tree, then dive back in to uncover the other " + (left == 1 ? "ending." : "endings.");
            }
            else
            {
                finalMessage.text = "You found all " + total + " endings! Spend your " + echoesNow + " Echoes in the upgrade tree and keep the Chain open.";
            }
            finalStats.text = "Days  <b>" + (DayManager.I != null ? DayManager.I.Day : 0) + "</b>\n"
                            + "Static popped  <b>" + s.blobsPopped.ToString("N0") + "</b>\n"
                            + "Highest combo  <b>x" + s.bestCombo + "</b>\n"
                            + "Total Data  <b>" + NumberFormat.Format(GameManager.I.LifetimeData) + "</b>\n"
                            + "Events solved  <b>" + s.eventsSolved + "</b>\n"
                            + "Time  <b>" + NumberFormat.Time(s.playSeconds) + "</b>";
            int echoes = GameManager.I.LastEchoAward;
            rebroadcastLabel.text = "REBROADCAST  (+" + echoes + " ECHO" + (echoes == 1 ? "" : "ES") + ")";
            UIFactory.Fade(finalGroup, true, 0.4f);
            Juice.StampIn(finalGroup.transform, 0.45f);
            AudioManager.Play("solved");
            VFXPlayer.ConfettiScreen(150);
        }

        private IEnumerator ShowCard(string text, Color tint)
        {
            advance = false;
            if (cardSymbols != null) Destroy(cardSymbols.gameObject);
            bool symbolic = SymbolText.IsSymbolic(text);
            if (symbolic)
            {
                cardText.text = "";
                cardSymbols = SymbolText.Build(cardHolder, text, 140f);
                AudioManager.Play("golden");
            }
            else
            {
                cardText.text = text;
                cardText.color = Color.white;
                cardText.ForceMeshUpdate();
                int total = cardText.textInfo.characterCount;
                float shown = 0f;
                cardText.maxVisibleCharacters = 0;
                while (shown < total && !advance)
                {
                    shown += Time.unscaledDeltaTime * 45f;
                    int prev = cardText.maxVisibleCharacters;
                    cardText.maxVisibleCharacters = (int)shown;
                    if ((int)shown / 3 != prev / 3) AudioManager.PlayVaried("type", 0.8f, 1.1f, 0.3f);
                    yield return null;
                }
                cardText.maxVisibleCharacters = total;
                advance = false;
            }
            hint.gameObject.SetActive(true);
            float wait = 0f;
            while (!advance && wait < 7f)
            {
                wait += Time.unscaledDeltaTime;
                hint.alpha = 0.4f + 0.6f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 2f));
                if (InputReader.I != null && InputReader.I.AnyKeyPressed) advance = true;
                yield return null;
            }
            advance = false;
            hint.gameObject.SetActive(false);
            float t = 0f;
            while (t < 0.3f)
            {
                t += Time.unscaledDeltaTime;
                cardText.alpha = 1f - t / 0.3f;
                yield return null;
            }
            cardText.alpha = 1f;
            cardText.text = "";
        }

        private IEnumerator WorldMoment(string id)
        {
            GameManager gm = GameManager.I;
            switch (id)
            {
                case "clear_channel":
                    // The Static is scrubbed for good. Clean and silent.
                    if (AudioManager.I != null) AudioManager.I.SetMusicDuck(0.08f);
                    yield return new WaitForSecondsRealtime(0.6f);
                    BlobSpawner.I.ScrubAll();
                    AudioManager.Play("crunch", 0.6f);
                    Juice.Shake(0.15f, 0.5f);
                    yield return new WaitForSecondsRealtime(2f);
                    break;

                case "become_signal":
                    {
                        Station st = gm.Station;
                        Vector3 top = st != null ? st.AntennaTop : Vector3.up * 4f;
                        BlobSpawner.I.SwarmTo(top);
                        AudioManager.Play("golden");
                        yield return new WaitForSecondsRealtime(1.6f);
                        if (st != null) StartCoroutine(Lift(st));
                        StartCoroutine(NebulaGlow());
                        yield return new WaitForSecondsRealtime(3.5f);
                        break;
                    }

                default:
                    // First Contact: fireworks of every blob color.
                    fireworks = true;
                    StartCoroutine(Fireworks());
                    yield return new WaitForSecondsRealtime(0.5f);
                    foreach (Blob b in BlobSpawner.I.Active.ToArray())
                    {
                        if (b.State == BlobState.Compressed) continue;
                        VFXPlayer.Pop(b.Position, BlobActions.ColorOf(b), 20);
                        BlobSpawner.I.Despawn(b);
                    }
                    yield return new WaitForSecondsRealtime(2.5f);
                    break;
            }
        }

        private IEnumerator Fireworks()
        {
            Camera cam = GameManager.I.Cam;
            while (fireworks)
            {
                Vector3 p = cam.transform.position + cam.transform.forward * Random.Range(10f, 18f)
                          + cam.transform.right * Random.Range(-8f, 8f) + Vector3.up * Random.Range(2f, 7f);
                VFXPlayer.Confetti(p, 48);
                VFXPlayer.Sparkle(p, 12, Palette.Rainbow(Random.value));
                AudioManager.PlayVaried("pop", 0.5f, 0.8f, 0.5f);
                yield return new WaitForSecondsRealtime(Random.Range(0.25f, 0.55f));
            }
        }

        private IEnumerator Lift(Station st)
        {
            Vector3 last = st.transform.position;
            Camera cam = GameManager.I.Cam;
            Quaternion camStart = cam.transform.rotation;
            Quaternion camEnd = camStart * Quaternion.Euler(-14f, 0f, 0f);
            float t = 0f;
            IEnumerator lift = st.LiftOff(8f);
            while (lift.MoveNext())
            {
                Vector3 now = st.transform.position;
                Vector3 delta = now - last;
                last = now;
                foreach (Blob b in BlobSpawner.I.Active) if (b.State == BlobState.Arranged) b.ShiftArrange(delta);
                t += Time.deltaTime;
                cam.transform.rotation = Quaternion.Slerp(camStart, camEnd, Juice.EaseInOut(Mathf.Clamp01(t / 8f)));
                if (Random.value < 0.3f) VFXPlayer.Sparkle(now + Random.insideUnitSphere * 1.5f, 3, Palette.Rainbow(Random.value));
                yield return lift.Current;
            }
        }

        private IEnumerator NebulaGlow()
        {
            if (RenderSettings.skybox == null) yield break;
            skyInstance = new Material(RenderSettings.skybox);
            RenderSettings.skybox = skyInstance;
            float start = skyInstance.HasProperty("_NebulaIntensity") ? skyInstance.GetFloat("_NebulaIntensity") : 1f;
            float t = 0f;
            while (t < 6f)
            {
                t += Time.unscaledDeltaTime;
                if (skyInstance.HasProperty("_NebulaIntensity")) skyInstance.SetFloat("_NebulaIntensity", Mathf.Lerp(start, start * 3.2f, t / 6f));
                yield return null;
            }
        }

        private void OnRebroadcast()
        {
            AudioManager.Play("skill");
            GameManager.I.Rebroadcast();
        }

        private void OnKeepPlaying()
        {
            UIFactory.Fade(finalGroup, false, 0.2f);
            UIFactory.Fade(group, false, 0.4f);
            backdrop.color = new Color(0.01f, 0.02f, 0.06f, 0f);
            GameManager.I.ContinueAfterEnding();
        }
    }
}
