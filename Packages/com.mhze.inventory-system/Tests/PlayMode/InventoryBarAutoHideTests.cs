using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using MHZE.PickupSystem;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using PickupSystemComponent = MHZE.PickupSystem.PickupSystem;

// Play mode: the bar that slides off the screen when nothing changed, comes back for every change and clears the way for
// other UI while it is there. Real time, the real update loop, a virtual keyboard.
namespace MHZE.InventorySystem.Tests
{
    public sealed class InventoryBarAutoHideTests : InputTestFixture
    {
        GameObject rig;
        PickupSystemComponent pickup;
        PlayerInventory bag;
        InventoryBarView view;
        Keyboard keyboard;
        PickableItemData apple, cube, bowl;
        readonly List<Object> made = new List<Object>();

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
        }

        public override void TearDown()
        {
            foreach (Object o in made) if (o != null) Object.Destroy(o);
            if (rig != null) Object.Destroy(rig);
            made.Clear();
            base.TearDown();
        }

        // ─────────────── building blocks ───────────────

        PickableItemData Data(string name, int maxStack)
        {
            var data = ScriptableObject.CreateInstance<PickableItemData>();
            data.name = name;
            data.ItemName = name;
            data.IsPickable = true;
            data.MaxStack = maxStack;
            made.Add(data);
            return data;
        }

        void BuildRig(int slots = 6, float visibleSeconds = 0.6f)
        {
            apple = Data("Apple", 3);
            cube = Data("Cube", 1);
            bowl = Data("Bowl", 1);

            rig = new GameObject("Rig");
            rig.SetActive(false);
            pickup = rig.AddComponent<PickupSystemComponent>();

            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            made.Add(asset);
            InputAction drop = asset.AddActionMap("Player").AddAction("Drop", InputActionType.Button, "<Keyboard>/q");
            typeof(PickupSystemComponent).GetField("DropInputAction", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(pickup, InputActionReference.Create(drop));

            var arm = new GameObject("Arm").transform;
            arm.SetParent(rig.transform, false);
            var socket = new GameObject("Socket").transform;
            socket.SetParent(arm, false);
            pickup.ConfigureHoldingRig(arm, socket);

            bag = rig.AddComponent<PlayerInventory>();
            typeof(PlayerInventory).GetField("slotCount", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(bag, slots);
            bag.DrawDelay = 0f;
            rig.SetActive(true);

            view = rig.GetComponent<InventoryBarView>();
            Assert.That(view, Is.Not.Null, "the inventory builds its bar");
            view.VisibleSeconds = visibleSeconds;
        }

        PickableItemBase Item(string name, PickableItemData data)
        {
            var go = new GameObject(name);
            go.transform.position = new Vector3(0f, 0f, 2f);
            go.AddComponent<Rigidbody>();
            go.AddComponent<BoxCollider>();
            var item = go.AddComponent<PickableItemBase>();
            item.pickableItemData = data;
            made.Add(go);
            return item;
        }

        IEnumerator PressKey(UnityEngine.InputSystem.Controls.KeyControl key)
        {
            Press(key);
            yield return null;
            Release(key);
            yield return null;
        }

        Canvas BarCanvas() => view.transform.Find("Inventory Bar").GetComponent<Canvas>();

        // Waits for a condition, and fails the test with what it was waiting for when it takes too long.
        IEnumerator Until(System.Func<bool> condition, float seconds, string what)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > end) Assert.Fail("Timed out waiting for: " + what + " (reveal " + view.Reveal.ToString("0.000") + ")");
                yield return null;
            }
        }

        IEnumerator UntilAway(string what = "the bar slides away on its own") => Until(() => view.Reveal == 0f, 5f, what);

        IEnumerator UntilThere(string what) => Until(() => view.Reveal > 0.99f, 1.5f, what);

        // ─────────────── the bar ───────────────

        [UnityTest]
        public IEnumerator TheBarStartsAwayComesInForAPickupAndLeavesAgain()
        {
            BuildRig();
            yield return null;
            yield return null;
            Assert.That(view.Reveal, Is.EqualTo(0f), "nothing has happened yet, so nothing is shown");
            Assert.That(view.IsOnScreen, Is.False);
            Assert.That(BarCanvas().enabled, Is.False, "nothing is drawn while the bar is away");

            Item("Apple", apple).Pickup();
            yield return UntilThere("the bar slides in for a pickup");
            Assert.That(BarCanvas().enabled, Is.True);
            Assert.That(view.IsOnScreen, Is.True);

            yield return UntilAway();
            Assert.That(BarCanvas().enabled, Is.False);
            Assert.That(bag.Inventory[0].Count, Is.EqualTo(1), "hiding the bar does not touch the items");
        }

        [UnityTest]
        public IEnumerator EverySwitchBringsTheBarBackAndRestartsTheWait()
        {
            BuildRig(visibleSeconds: 0.7f);
            yield return null;
            Item("A", cube).Pickup(); yield return null;
            Item("B", bowl).Pickup(); yield return null;
            yield return UntilAway();

            yield return PressKey(keyboard.digit1Key);
            Assert.That(bag.SelectedIndex, Is.EqualTo(0));
            yield return UntilThere("the bar slides in for a switch of slot");

            // Switching every 0.3 s keeps it open well past the 0.7 s a single change would keep it.
            float end = Time.realtimeSinceStartup + 2f;
            int turn = 0;
            while (Time.realtimeSinceStartup < end)
            {
                yield return PressKey(turn++ % 2 == 0 ? keyboard.digit2Key : keyboard.digit1Key);
                float next = Time.realtimeSinceStartup + 0.3f;
                while (Time.realtimeSinceStartup < next)
                {
                    Assert.That(view.Reveal, Is.GreaterThan(0.99f), "the bar stays while the player keeps changing things");
                    yield return null;
                }
            }

            yield return UntilAway("the bar leaves a while after the last change");
        }

        [UnityTest]
        public IEnumerator TheWheelAlsoBringsTheBarBack()
        {
            BuildRig();
            yield return null;
            Item("A", cube).Pickup(); yield return null;
            Item("B", bowl).Pickup(); yield return null;
            yield return UntilAway();

            Mouse mouse = Mouse.current;
            Set(mouse.scroll, new Vector2(0f, 120f));
            yield return null;
            Set(mouse.scroll, Vector2.zero);
            yield return null;

            Assert.That(bag.SelectedIndex, Is.EqualTo(0), "scrolling up selects the previous slot");
            yield return UntilThere("the bar slides in for the wheel");
        }

        [UnityTest]
        public IEnumerator ADropBringsTheBarBack()
        {
            BuildRig();
            yield return null;
            Item("A", cube).Pickup(); yield return null;
            yield return UntilAway();

            yield return PressKey(keyboard.qKey);
            yield return UntilThere("the bar slides in for a drop");
            Assert.That(bag.Inventory[0].IsEmpty, Is.True, "the item left the bar");
        }

        [UnityTest]
        public IEnumerator PressingTheKeyOfTheSelectedSlotShowsTheBarThoughNothingChanges()
        {
            BuildRig();
            yield return null;
            Item("A", cube).Pickup(); yield return null;
            yield return UntilAway();

            int repeated = 0;
            bag.SelectionRepeated += () => repeated++;
            yield return PressKey(keyboard.digit1Key);

            Assert.That(repeated, Is.EqualTo(1));
            Assert.That(bag.SelectedIndex, Is.EqualTo(0));
            yield return UntilThere("the bar slides in for the key of the slot that is already selected");
        }

        [UnityTest]
        public IEnumerator ARefusedPickupShowsTheBarAndTheNotice()
        {
            BuildRig(slots: 2);
            yield return null;
            Item("A", cube).Pickup(); yield return null;
            Item("B", bowl).Pickup(); yield return null;
            yield return UntilAway();

            Item("Extra", apple).Pickup();
            yield return UntilThere("the bar slides in when a pickup is refused");
            TMP_Text notice = view.transform.Find("Inventory Bar/Bar/Notice").GetComponent<TMP_Text>();
            Assert.That(notice.text, Is.EqualTo("Inventory full"));
            Assert.That(notice.color.a, Is.GreaterThan(0.5f));
        }

        [UnityTest]
        public IEnumerator ASwitchRefusedBecauseTheHandHoldsAStoryItemShowsTheBar()
        {
            BuildRig();
            yield return null;
            Item("A", cube).Pickup(); yield return null;
            PickableItemBase brush = Item("Brush", bowl);
            pickup.PickupObject(brush.gameObject, brush);
            yield return null;
            Assert.That(bag.IsHandLocked, Is.True);
            yield return UntilAway();

            yield return PressKey(keyboard.digit1Key);
            Assert.That(bag.SelectedIndex, Is.EqualTo(1), "the story item stays in the hand");
            yield return UntilThere("the bar slides in when a switch is refused");
        }

        [UnityTest]
        public IEnumerator ASlotCountChangeWhilePlayingBringsTheBarButTheFirstBuildDoesNot()
        {
            BuildRig();
            yield return null;
            yield return null;
            Assert.That(view.Reveal, Is.EqualTo(0f), "the first build is not a change");

            bag.SetSlotCount(8);
            yield return UntilThere("the bar slides in when the number of slots changes");
        }

        [UnityTest]
        public IEnumerator WithAutoHideOffTheBarStaysOnScreen()
        {
            BuildRig(visibleSeconds: 0.3f);
            view.AutoHide = false;
            yield return UntilThere("the bar is on screen without any change");

            yield return new WaitForSeconds(0.8f);
            Assert.That(view.Reveal, Is.EqualTo(1f), "it does not leave");

            view.AutoHide = true;
            yield return UntilAway("it leaves once auto hide is on");
        }

        [UnityTest]
        public IEnumerator AFreshRunStartsFromNothing()
        {
            // Fast Play keeps the non-serialized fields of the last run while the clock starts over: a bar that was told to stay
            // for 30 s must not stay for the new run.
            BuildRig(visibleSeconds: 30f);
            yield return null;
            Item("A", cube).Pickup();
            yield return UntilThere("the bar slides in");

            typeof(InventoryBarView).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(view, null);
            yield return null;
            yield return null;
            Assert.That(view.Reveal, Is.EqualTo(0f), "the new run starts with the bar away");

            yield return new WaitForSeconds(0.6f);
            Assert.That(view.Reveal, Is.EqualTo(0f), "and the old timer does not bring it back");
        }

        [UnityTest]
        public IEnumerator ShowAndHideWorkByHand()
        {
            BuildRig();
            yield return null;
            view.Show(5f);
            yield return UntilThere("Show brings the bar in");

            view.Hide();
            yield return UntilAway("Hide sends it away");

            view.Show();
            yield return UntilThere("Show brings it in again");
        }

        [UnityTest]
        public IEnumerator TheBarSlidesSmoothlyAndTurnsRoundWithoutAJump()
        {
            BuildRig(visibleSeconds: 0.4f);
            yield return null;

            // Every frame of the whole run is looked at: the bar comes in, starts to leave, a change turns it round, it comes in again.
            float last = view.Reveal;
            float biggestStep = 0f;
            System.Func<System.Func<bool>, float, IEnumerator> until = (condition, seconds) => Watch(condition, seconds, () => { biggestStep = Mathf.Max(biggestStep, Mathf.Abs(view.Reveal - last)); last = view.Reveal; });

            Item("A", cube).Pickup();
            yield return until(() => view.Reveal > 0.99f, 1.5f);
            yield return until(() => view.Reveal < 0.7f, 3f);
            float whenTurned = view.Reveal;
            Assert.That(whenTurned, Is.GreaterThan(0.05f), "it is still on its way out");

            bag.Select(1);
            yield return until(() => view.Reveal > 0.99f, 1.5f);

            Assert.That(biggestStep, Is.LessThan(0.6f), "it moved in steps, not in jumps");
        }

        // Runs a step every frame until a condition holds.
        IEnumerator Watch(System.Func<bool> condition, float seconds, System.Action everyFrame)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > end) Assert.Fail("Timed out (reveal " + view.Reveal.ToString("0.000") + ")");
                yield return null;
                everyFrame();
            }
        }

        // ─────────────── UI that rides above the bar ───────────────

        RectTransform SubtitlePanel(float y, bool avoid)
        {
            var canvasObject = new GameObject("Subtitle canvas", typeof(RectTransform), typeof(Canvas));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            made.Add(canvasObject);

            var panel = new GameObject("Panel", typeof(RectTransform));
            var rect = (RectTransform)panel.transform;
            rect.SetParent(canvasObject.transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(400f, 70f);
            rect.anchoredPosition = new Vector2(0f, y);
            if (avoid) panel.AddComponent<InventoryBarAvoider>();
            return rect;
        }

        static float LowestScreenY(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            float lowest = float.MaxValue;
            foreach (Vector3 corner in corners) lowest = Mathf.Min(lowest, corner.y);
            return lowest;
        }

        [UnityTest]
        public IEnumerator SubtitlesRideAboveTheBarAndDropBackWhenItLeaves()
        {
            BuildRig();
            RectTransform panel = SubtitlePanel(10f, avoid: true);
            yield return null;
            yield return null;
            Assert.That(panel.anchoredPosition.y, Is.EqualTo(10f).Within(0.01f), "the bar is away: the subtitles rest where they were put");

            Item("A", cube).Pickup();
            yield return UntilThere("the bar slides in");
            yield return null;
            Assert.That(panel.anchoredPosition.y, Is.GreaterThan(10f), "the subtitles made room");
            Assert.That(LowestScreenY(panel), Is.GreaterThanOrEqualTo(view.ClearanceHeight - 0.5f), "and sit above the bar and its notice");

            yield return UntilAway();
            yield return null;
            Assert.That(panel.anchoredPosition.y, Is.EqualTo(10f).Within(0.01f), "the subtitles use the space again");
        }

        [UnityTest]
        public IEnumerator TheSubtitlesMakeRoomForTheNoticeOnlyWhileItShows()
        {
            BuildRig(slots: 2, visibleSeconds: 4f);
            RectTransform panel = SubtitlePanel(10f, avoid: true);
            yield return null;
            Item("A", cube).Pickup(); yield return null;
            Item("B", bowl).Pickup(); yield return null;
            yield return UntilThere("the bar slides in");
            yield return new WaitForSeconds(0.3f);
            float withoutNotice = view.ClearanceHeight;
            float raisedWithout = panel.anchoredPosition.y;

            Item("Extra", apple).Pickup();
            yield return new WaitForSeconds(0.5f);
            Assert.That(view.ClearanceHeight, Is.GreaterThan(withoutNotice + 1f), "the notice needs room above the bar");
            Assert.That(panel.anchoredPosition.y, Is.GreaterThan(raisedWithout + 1f), "so the subtitles rise a little more");
            Assert.That(LowestScreenY(panel), Is.GreaterThanOrEqualTo(view.ClearanceHeight - 0.5f), "and stay above the notice");

            yield return Until(() => Mathf.Approximately(view.ClearanceHeight, withoutNotice), 4f, "the room is given back once the notice is gone");
            yield return null;
            Assert.That(panel.anchoredPosition.y, Is.EqualTo(raisedWithout).Within(0.5f), "and the subtitles settle back above the bar");
        }

        [UnityTest]
        public IEnumerator SubtitlesAlreadyAboveTheBarAreLeftAlone()
        {
            BuildRig();
            RectTransform panel = SubtitlePanel(900f, avoid: true);
            yield return null;

            Item("A", cube).Pickup();
            yield return UntilThere("the bar slides in");
            yield return null;
            Assert.That(panel.anchoredPosition.y, Is.EqualTo(900f).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator SwitchingTheAvoiderOffPutsTheSubtitlesBack()
        {
            BuildRig();
            RectTransform panel = SubtitlePanel(10f, avoid: true);
            yield return null;

            Item("A", cube).Pickup();
            yield return UntilThere("the bar slides in");
            yield return null;
            Assert.That(panel.anchoredPosition.y, Is.GreaterThan(10f));

            panel.GetComponent<InventoryBarAvoider>().enabled = false;
            Assert.That(panel.anchoredPosition.y, Is.EqualTo(10f).Within(0.01f), "put back where it was authored");
        }
    }
}
