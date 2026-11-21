using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace MHZE.InteractSystem.Tests
{
    public sealed class PlayerInteractorHoldTests
    {
        private GameObject player, firstObject, secondObject;
        private Camera camera;
        private PlayerInteractor interactor;
        private InteractableItemBase first, second;
        private InputAction action;
        private InputActionAsset asset;
        private InputActionReference reference;
        private Keyboard keyboard;
        private InputSettings originalSettings, settings;
        private string settingsSnapshot;
        private int firstCount, secondCount, holdEnds;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            originalSettings = InputSystem.settings;
            settingsSnapshot = JsonUtility.ToJson(originalSettings);
            settings = Object.Instantiate(originalSettings);
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = settings;
            keyboard = InputSystem.AddDevice<Keyboard>();
            asset = ScriptableObject.CreateInstance<InputActionAsset>();
            var map = new InputActionMap("Test"); asset.AddActionMap(map);
            map.devices = new InputDevice[] { keyboard };
            action = map.AddAction("Interact", InputActionType.Button, "<Keyboard>/e");
            reference = InputActionReference.Create(action);
            player = new GameObject("Hold test player");
            camera = player.AddComponent<Camera>(); camera.enabled = false;
            interactor = player.AddComponent<PlayerInteractor>(); interactor.enabled = false;
            SetField(interactor, "playerCamera", camera);
            SetField(interactor, "interactInputAction", reference);
            SetField(interactor, "showDebugRay", false);
            first = MakeTarget("First", new Vector3(0, 0, 2), out firstObject);
            second = MakeTarget("Second", new Vector3(2, 0, 2), out secondObject);
            firstCount = secondCount = holdEnds = 0;
            first.Interacted += _ => firstCount++;
            second.Interacted += _ => secondCount++;
            interactor.OnHoldAttemptEnded += () => holdEnds++;
            interactor.enabled = true;
            Physics.SyncTransforms();
            yield return null; yield return null;
            Assert.That(interactor.CurrentInteractable, Is.SameAs(first));
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (interactor) interactor.enabled = false;
            action?.Dispose();
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (!originalSettings)
            {
                originalSettings = ScriptableObject.CreateInstance<InputSettings>();
                JsonUtility.FromJsonOverwrite(settingsSnapshot, originalSettings);
            }
            InputSystem.settings = originalSettings;
            Object.Destroy(settings); Object.Destroy(reference); Object.Destroy(asset);
            Object.Destroy(player); Object.Destroy(firstObject); Object.Destroy(secondObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ContinuousHoldCompletesExactlyOnce()
        {
            Press(); yield return new WaitForSeconds(1.1f);
            Assert.That(firstCount, Is.EqualTo(1));
            Assert.That(holdEnds, Is.EqualTo(1));
            yield return new WaitForSeconds(.3f);
            Assert.That(firstCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator EarlyReleaseCancelsAndRequiresAnotherFullHold()
        {
            Press(); yield return new WaitForSeconds(.15f); Release();
            yield return new WaitForSeconds(1);
            Assert.That(firstCount, Is.Zero);
            Assert.That(holdEnds, Is.EqualTo(1));
            Press(); yield return new WaitForSeconds(1.1f);
            Assert.That(firstCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator LookingAwayDoesNotResumeWhenLookingBack()
        {
            Press(); yield return new WaitForSeconds(.15f);
            camera.transform.rotation = Quaternion.Euler(0, 180, 0);
            yield return null; yield return null;
            Assert.That(interactor.CurrentInteractable, Is.Null);
            camera.transform.rotation = Quaternion.identity;
            yield return new WaitForSeconds(1.1f);
            Assert.That(firstCount, Is.Zero);
            Assert.That(holdEnds, Is.EqualTo(1));
            Release(); Press(); yield return new WaitForSeconds(1.1f);
            Assert.That(firstCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator SwitchingTargetsDoesNotTransferTheHold()
        {
            Press(); yield return new WaitForSeconds(.15f);
            camera.transform.rotation = Quaternion.Euler(0, 45, 0);
            yield return new WaitForSeconds(1.1f);
            Assert.That(interactor.CurrentInteractable, Is.SameAs(second));
            Assert.That(firstCount, Is.Zero); Assert.That(secondCount, Is.Zero);
            Assert.That(holdEnds, Is.EqualTo(1));
            Release(); Press(); yield return new WaitForSeconds(1.1f);
            Assert.That(secondCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator BecomingUnavailableCancelsTheHold()
        {
            Press(); yield return new WaitForSeconds(.15f);
            first.SetIsInteractable(false); yield return null; yield return null;
            first.SetIsInteractable(true); yield return new WaitForSeconds(1.1f);
            Assert.That(firstCount, Is.Zero);
            Assert.That(holdEnds, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator SolidWallBetweenCameraAndTargetBlocksIt()
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0, 0, 1); wall.transform.localScale = new Vector3(4, 4, .2f);
            Physics.SyncTransforms();
            yield return null; yield return null;
            Assert.That(interactor.CurrentInteractable, Is.Null);
            Object.Destroy(wall); yield return null; yield return null;
            Assert.That(interactor.CurrentInteractable, Is.SameAs(first));
        }

        [UnityTest]
        public IEnumerator WallOnAnotherLayerStillBlocksWhenInteractablesAreOnOneLayer()
        {
            // The project's player only lists the interactable layer, so a wall is on a layer the interactable mask omits.
            firstObject.layer = 8;
            SetField(interactor, "interactableLayer", (LayerMask)(1 << 8));
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0, 0, 1); wall.transform.localScale = new Vector3(4, 4, .2f);
            Physics.SyncTransforms();
            yield return null; yield return null;
            Assert.That(interactor.CurrentInteractable, Is.Null);
            Object.Destroy(wall); yield return null; yield return null;
            Assert.That(interactor.CurrentInteractable, Is.SameAs(first));
        }

        [UnityTest]
        public IEnumerator TriggerVolumeInFrontDoesNotBlock()
        {
            var volume = new GameObject("Trigger volume"); volume.transform.position = new Vector3(0, 0, 1);
            volume.AddComponent<BoxCollider>().isTrigger = true;
            Physics.SyncTransforms();
            yield return null; yield return null;
            Assert.That(interactor.CurrentInteractable, Is.SameAs(first));
            Object.Destroy(volume);
        }

        [UnityTest]
        public IEnumerator TargetBeyondMaxDistanceIsNotInteractable()
        {
            firstObject.transform.position = new Vector3(0, 0, 6);
            Physics.SyncTransforms();
            yield return null; yield return null;
            Assert.That(interactor.CurrentInteractable, Is.Null);
        }

        private static InteractableItemBase MakeTarget(string name, Vector3 position, out GameObject obj)
        {
            obj = new GameObject(name); obj.transform.position = position;
            obj.AddComponent<BoxCollider>();
            var target = obj.AddComponent<InteractableItemBase>();
            SetField(target, "holdTime", .8f);
            return target;
        }
        private static void SetField(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private void Press() { InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E)); InputSystem.Update(); }
        private void Release() { InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update(); }
    }
}
