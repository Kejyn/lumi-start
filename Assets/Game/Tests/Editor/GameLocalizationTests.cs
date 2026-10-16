using System.Linq;
using Lumivale.Start.Game;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

namespace Lumivale.Start.GameTests
{
    public sealed class GameLocalizationTests
    {
        [TestCase(SystemLanguage.French, "fr")]
        [TestCase(SystemLanguage.English, "en")]
        [TestCase(SystemLanguage.Spanish, "en")]
        [TestCase(SystemLanguage.German, "en")]
        [TestCase(SystemLanguage.Russian, "en")]
        [TestCase(SystemLanguage.ChineseSimplified, "en")]
        [TestCase(SystemLanguage.Unknown, "en")]
        public void SystemLanguageSelectionHasDeterministicSupportedFallback(SystemLanguage language, string expected)
        {
            Assert.That(GameLocalizationService.DetectLanguage(language), Is.EqualTo(expected));
        }

        [Test]
        public void EveryAvailableLocaleHasEveryNonEmptyTranslation()
        {
            var tables = AssetDatabase.FindAssets("t:StringTable", new[] { "Assets/Game/Localization" })
                .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<StringTable>)
                .Where(t => t != null).ToArray();
            Assert.That(tables.Select(t => t.LocaleIdentifier.Code), Is.EquivalentTo(new[] { "en", "fr" }));
            var referenceKeys = tables[0].SharedData.Entries.Select(e => e.Key).ToArray();
            Assert.That(referenceKeys.Length, Is.GreaterThan(100), "The entire Starter must be represented, including dynamic strings.");
            foreach (var table in tables)
            {
                Assert.That(table.Values.Count, Is.EqualTo(referenceKeys.Length));
                foreach (var key in referenceKeys)
                    Assert.That(table.GetEntry(key)?.Value, Is.Not.Null.And.Not.Empty, table.LocaleIdentifier.Code + ": " + key);
            }
        }

        [Test]
        public void SettingsAreAsynchronousAndDoNotOfferUntranslatedFutureLanguages()
        {
            var settings = AssetDatabase.LoadAssetAtPath<LocalizationSettings>("Assets/Game/Localization/LocalizationSettings.asset");
            Assert.That(settings, Is.Not.Null);
            Assert.That(LocalizationSettings.InitializeSynchronously, Is.False);
            Assert.That(settings.GetAvailableLocales().Locales.Select(l => l.Identifier.Code), Is.EquivalentTo(new[] { "en", "fr" }));
            Assert.That(settings.GetStartupLocaleSelectors().Any(s => s is PlayerPrefLocaleSelector), Is.False,
                "The existing Settings key owns confirmed preferences; preview must not write an extra key.");
        }
    }
}
