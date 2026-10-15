using System;
using System.Collections.Generic;
using Lumivale.Core.Bootstrap;
using Lumivale.Core.Input;
using Lumivale.Start.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Lumivale.Start.GameTests
{
    /// <summary>Real PlayerPrefs with fixture-owned keys; no default preference or save data is touched.</summary>
    public sealed class GamePreferencesTests
    {
        private string key;
        private InputActionAsset source;
        private BootstrapCoordinator coordinator;
        private InputService input;

        [SetUp]
        public void SetUp() => key = "lumivale.product.preferences.test." + Guid.NewGuid().ToString("N");

        [TearDown]
        public void TearDown()
        {
            coordinator?.ShutdownAsync().GetAwaiter().GetResult();
            if (source != null) UnityEngine.Object.DestroyImmediate(source);
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.DeleteKey(key + GamePreferences.PresentationSuffix);
            PlayerPrefs.DeleteKey(key + GamePreferences.BindingsSuffix);
            PlayerPrefs.Save();
        }

        [Test]
        public void DefaultsDoNotCreateKeysOrChangeTheExistingSettingsDocument()
        {
            PlayerPrefs.SetString(key, "fixture-owned Core Settings document");
            var store = new GamePreferences(key);
            Assert.That(store.Current.MovementSensitivity, Is.EqualTo(1f));
            Assert.That(store.Current.InterfaceScale, Is.EqualTo(1f));
            Assert.That(store.Current.ShowHints, Is.True);
            Assert.That(PlayerPrefs.HasKey(store.PresentationKey), Is.False);
            Assert.That(PlayerPrefs.HasKey(store.BindingsKey), Is.False);
            Assert.That(PlayerPrefs.GetString(key), Is.EqualTo("fixture-owned Core Settings document"));
        }

        [Test]
        public void ExplicitSaveSurvivesANewStoreAndDoesNotShareMutableValues()
        {
            var store = new GamePreferences(key);
            var values = new GamePresentationValues
            { MovementSensitivity = 2f, InvertY = true, InterfaceScale = .9f, ShowHints = false };
            store.SavePresentation(values);
            values.MovementSensitivity = 1f;
            var loaded = new GamePreferences(key).Current;
            Assert.That(loaded.MovementSensitivity, Is.EqualTo(2f));
            Assert.That(loaded.InvertY, Is.True);
            Assert.That(loaded.InterfaceScale, Is.EqualTo(.9f));
            Assert.That(loaded.ShowHints, Is.False);
            loaded.InterfaceScale = 1.2f;
            Assert.That(store.Current.InterfaceScale, Is.EqualTo(.9f));
        }

        [TestCase(.24f, 1f)]
        [TestCase(3.01f, 1f)]
        [TestCase(1f, .79f)]
        [TestCase(1f, 1.21f)]
        [TestCase(float.NaN, 1f)]
        [TestCase(1f, float.PositiveInfinity)]
        public void InvalidRangesAreRejectedBeforeAnyWrite(float sensitivity, float scale)
        {
            var store = new GamePreferences(key);
            Assert.Throws<ArgumentException>(() => store.SavePresentation(new GamePresentationValues
            { MovementSensitivity = sensitivity, InterfaceScale = scale }));
            Assert.That(PlayerPrefs.HasKey(store.PresentationKey), Is.False);
        }

        [TestCase("{\"Version\":2,\"Values\":{\"MovementSensitivity\":1,\"InterfaceScale\":1}}")]
        [TestCase("{truncated")]
        [TestCase("{\"Version\":1,\"Values\":{\"MovementSensitivity\":8,\"InterfaceScale\":1}}")]
        public void InvalidOrFuturePresentationRemainsIntactUntilExplicitApply(string original)
        {
            string preference = key + GamePreferences.PresentationSuffix;
            PlayerPrefs.SetString(preference, original);
            var store = new GamePreferences(key);
            Assert.That(store.Current.MovementSensitivity, Is.EqualTo(1f));
            Assert.That(store.PresentationDiagnostic, Is.Not.Empty);
            Assert.That(PlayerPrefs.GetString(preference), Is.EqualTo(original));
            store.SavePresentation(new GamePresentationValues { MovementSensitivity = 1.5f });
            Assert.That(store.PresentationDiagnostic, Is.Null);
            Assert.That(new GamePreferences(key).Current.MovementSensitivity, Is.EqualTo(1.5f));
        }

        [Test]
        public void OversizedPresentationIsPreservedWithoutAutomaticRepair()
        {
            string original = new string('x', 4097);
            string preference = key + GamePreferences.PresentationSuffix;
            PlayerPrefs.SetString(preference, original);
            var store = new GamePreferences(key);
            Assert.That(store.PresentationDiagnostic, Is.Not.Empty);
            Assert.That(PlayerPrefs.GetString(preference), Is.EqualTo(original));
        }

        [Test]
        public void GameplayOverridesReloadOnceAndNeverModifySourceBindings()
        {
            CreateInput();
            input.GetAction("Gameplay/Counter").ApplyBindingOverride(0, "<Keyboard>/e");
            var store = new GamePreferences(key);
            store.SaveBindings(input);
            input.ResetBindingOverrides();
            var freshStore = new GamePreferences(key);
            freshStore.LoadBindingsOnce(input);
            Assert.That(input.GetAction("Gameplay/Counter").bindings[0].effectivePath, Is.EqualTo("<Keyboard>/e"));
            input.GetAction("Gameplay/Counter").ApplyBindingOverride(0, "<Keyboard>/f");
            freshStore.LoadBindingsOnce(input);
            Assert.That(input.GetAction("Gameplay/Counter").bindings[0].effectivePath, Is.EqualTo("<Keyboard>/f"));
            Assert.That(source.FindAction("Gameplay/Counter").bindings[0].overridePath, Is.Null);
            Assert.That(source.FindAction("Gameplay/Counter").bindings[0].path, Is.EqualTo("<Keyboard>/c"));
        }

        [TestCase("{\"Version\":2,\"Overrides\":\"\"}")]
        [TestCase("{truncated")]
        [TestCase("{\"Version\":1,\"Overrides\":\"{truncated\"}")]
        public void InvalidOrFutureBindingDocumentIsPreserved(string original)
        {
            CreateInput();
            var store = new GamePreferences(key);
            PlayerPrefs.SetString(store.BindingsKey, original);
            store.LoadBindingsOnce(input);
            Assert.That(store.BindingDiagnostic, Is.Not.Empty);
            Assert.That(input.GetAction("Gameplay/Counter").bindings[0].effectivePath, Is.EqualTo("<Keyboard>/c"));
            Assert.That(PlayerPrefs.GetString(store.BindingsKey), Is.EqualTo(original));
        }

        [Test]
        public void StoredUiOverridesAreRejectedAndReservedMenuBindingsRemainUsable()
        {
            CreateInput();
            input.GetAction("UI/Submit").ApplyBindingOverride(0, "<Keyboard>/space");
            string overrides = input.ExportBindingOverrides();
            input.ResetBindingOverrides();
            var store = new GamePreferences(key);
            string original = "{\"Version\":1,\"Overrides\":" + JsonQuote(overrides) + "}";
            PlayerPrefs.SetString(store.BindingsKey, original);
            store.LoadBindingsOnce(input);
            Assert.That(store.BindingDiagnostic, Does.Contain("menu controls are reserved"));
            Assert.That(input.GetAction("UI/Submit").bindings[0].effectivePath, Is.EqualTo("<Keyboard>/enter"));
            Assert.That(PlayerPrefs.GetString(store.BindingsKey), Is.EqualTo(original));
        }

        [Test]
        public void ExplicitBindingSaveRejectsReservedMapOverridesBeforeWrite()
        {
            CreateInput();
            input.GetAction("UI/Submit").ApplyBindingOverride(0, "<Keyboard>/space");
            var store = new GamePreferences(key);
            Assert.Throws<FormatException>(() => store.SaveBindings(input));
            Assert.That(PlayerPrefs.HasKey(store.BindingsKey), Is.False);
        }

        private void CreateInput()
        {
            source = ScriptableObject.CreateInstance<InputActionAsset>();
            var gameplay = source.AddActionMap("Gameplay");
            gameplay.AddAction("Counter", InputActionType.Button, "<Keyboard>/c");
            var ui = source.AddActionMap("UI");
            ui.AddAction("Submit", InputActionType.Button, "<Keyboard>/enter");
            input = new InputService(source, new Dictionary<InputContext, string[]>
            {
                [InputContext.Gameplay] = new[] { "Gameplay" },
                [InputContext.UI] = new[] { "UI" },
                [InputContext.Pause] = new[] { "UI" }
            }, InputContext.UI);
            coordinator = new BootstrapCoordinator(new ILumiService[] { input });
            coordinator.InitializeAsync().GetAwaiter().GetResult();
        }

        private static string JsonQuote(string text) => "\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }
}
