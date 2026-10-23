using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PokeHunter.Catalogue;

namespace PokeHunter.Tests.Catalogue
{
    internal static class Program
    {
        /// <summary>
        /// Runs catalogue loading and rejection checks.
        ///
        /// Returns zero when every check passes, or one on failure.
        /// The original catalogue is never written to.
        /// </summary>
        private static int Main(string[] args)
        {
            string temporaryDirectory = null;

            try
            {
                if (args.Length != 1)
                {
                    throw new ArgumentException(
                        "Usage: CatalogueChecks.exe <catalogue-file>"
                    );
                }

                // Accept a convenient relative command-line argument,
                // then give the loader the absolute path it requires.
                string cataloguePath = Path.GetFullPath(args[0]);

                CatalogueDocument catalogue =
                    CatalogueLoader.Load(cataloguePath);

                int normalEntries = 0;
                int shinyEntries = 0;

                foreach (CatalogueEntry entry in catalogue.Entries)
                {
                    if (entry.IsShiny)
                    {
                        shinyEntries++;
                    }
                    else
                    {
                        normalEntries++;
                    }
                }

                Console.WriteLine(
                    "PASS: The real catalogue loaded and validated."
                );

                Console.WriteLine(
                    "Entries: {0} | Groups: {1} | Normal: {2} | Shiny: {3}",
                    catalogue.Entries.Count,
                    catalogue.EncounterGroups.Count,
                    normalEntries,
                    shinyEntries
                );

                // Keep an untouched JSON document to clone for each test.
                JObject original = JObject.Parse(
                    File.ReadAllText(cataloguePath)
                );

                temporaryDirectory = Path.Combine(
                    Path.GetTempPath(),
                    "PokeHunter-CatalogueChecks-" +
                    Guid.NewGuid().ToString("N")
                );

                Directory.CreateDirectory(temporaryDirectory);

                RunRejectionCheck(
                    original,
                    temporaryDirectory,
                    "unsupported-schema",
                    document => document["schemaVersion"] = 999,
                    "schemaVersion must be 1.",
                    "An unsupported schema version was rejected."
                );

                RunRejectionCheck(
                    original,
                    temporaryDirectory,
                    "missing-property",
                    document => FirstEntry(document).Remove("isShiny"),
                    "Required property 'isShiny' not found",
                    "A missing required property was rejected."
                );

                RunRejectionCheck(
                    original,
                    temporaryDirectory,
                    "unknown-property",
                    document =>
                        FirstEntry(document)["unexpectedProperty"] = true,
                    "unexpectedProperty",
                    "An unknown entry property was rejected."
                );

                RunRejectionCheck(
                    original,
                    temporaryDirectory,
                    "missing-counterpart",
                    document =>
                    {
                        JArray entries = ArrayProperty(document, "entries");
                        JObject first = FirstEntry(document);

                        string counterpartId =
                            first.Value<string>("formId") + ":" +
                            first.Value<string>("gender") + ":" +
                            (first.Value<bool>("isShiny")
                                ? "normal"
                                : "shiny");

                        JObject counterpart = FindEntry(
                            document,
                            counterpartId
                        );

                        entries.Remove(counterpart);
                    },
                    "is missing counterpart",
                    "A missing normal/shiny counterpart was rejected."
                );

                RunRejectionCheck(
                    original,
                    temporaryDirectory,
                    "counterpart-conflict",
                    document =>
                    {
                        JObject first = FirstEntry(document);

                        first["displayName"] =
                            first.Value<string>("displayName") +
                            " changed for test";
                    },
                    "have conflicting shared metadata",
                    "Conflicting counterpart metadata was rejected."
                );

                RunRejectionCheck(
                    original,
                    temporaryDirectory,
                    "unknown-rarity",
                    document =>
                        FirstGroup(document)["rarityClass"] = "legendary",
                    "unsupported rarity class",
                    "An unknown rarity class was rejected."
                );

                RunRejectionCheck(
                    original,
                    temporaryDirectory,
                    "unknown-entry-reference",
                    document =>
                    {
                        JArray entryIds = ArrayProperty(
                            FirstGroup(document),
                            "entryIds"
                        );

                        entryIds[0] = "missing-test-entry";
                    },
                    "references unknown entry",
                    "A group reference to an unknown entry was rejected."
                );

                RunRejectionCheck(
                    original,
                    temporaryDirectory,
                    "duplicate-entry",
                    document =>
                    {
                        JArray entries = ArrayProperty(document, "entries");
                        entries.Add(FirstEntry(document).DeepClone());
                    },
                    "Duplicate collection entry ID",
                    "A duplicate collection identity was rejected."
                );

                RunRejectionCheck(
                    original,
                    temporaryDirectory,
                    "duplicate-group",
                    document =>
                    {
                        JArray groups = ArrayProperty(
                            document,
                            "encounterGroups"
                        );

                        groups.Add(FirstGroup(document).DeepClone());
                    },
                    "Duplicate encounter group ID",
                    "A duplicate encounter group was rejected."
                );

                RunRejectionCheck(
                    original,
                    temporaryDirectory,
                    "ungrouped-entry",
                    document =>
                    {
                        // Remove a complete group, leaving its entries
                        // and their normal/shiny counterparts intact.
                        ArrayProperty(
                            document,
                            "encounterGroups"
                        ).RemoveAt(0);
                    },
                    "is not referenced by an encounter group",
                    "An entry without group membership was rejected."
                );

                RunMissingMediaCheck(original, temporaryDirectory);

                // Test reusable lookups without removing any existing loader checks.
                CatalogueIndexChecks.Run(catalogue);
                EligibilityChecks.Run(catalogue);
                WeightedSelectionChecks.Run();
                WeightedRollerChecks.Run();
                EncounterGroupSelectionChecks.Run(catalogue);
                ShinyProbabilityChecks.Run();
                ShinyRollerChecks.Run();
                EncounterOwnershipChecks.Run(catalogue);
                EncounterVariantChecks.Run();

                Console.WriteLine("All catalogue checks passed.");
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(
                    "FAIL: " + exception.Message
                );

                return 1;
            }
            finally
            {
                if (temporaryDirectory != null)
                {
                    try
                    {
                        Directory.Delete(temporaryDirectory, true);
                    }
                    catch (Exception exception)
                    {
                        // Cleanup trouble must be visible, but should
                        // not hide the original test result.
                        Console.Error.WriteLine(
                            "WARNING: Could not remove temporary files at '" +
                            temporaryDirectory + "': " + exception.Message
                        );
                    }
                }
            }
        }

        /// <summary>
        /// Changes an independent copy and checks the expected rejection.
        /// Every test starts from the same valid original document.
        /// </summary>
        private static void RunRejectionCheck(
            JObject original,
            string directory,
            string filename,
            Action<JObject> change,
            string expectedMessage,
            string successMessage
        )
        {
            JObject copy = (JObject)original.DeepClone();
            change(copy);

            string path = WriteTemporaryDocument(
                directory,
                filename,
                copy
            );

            try
            {
                CatalogueLoader.Load(path);
            }
            catch (InvalidDataException exception)
            {
                if (exception.Message.IndexOf(
                    expectedMessage,
                    StringComparison.OrdinalIgnoreCase
                ) < 0)
                {
                    throw new InvalidOperationException(
                        filename + " failed for an unexpected reason: " +
                        exception.Message,
                        exception
                    );
                }

                Console.WriteLine("PASS: " + successMessage);
                return;
            }

            throw new InvalidOperationException(
                filename + " was accepted but should have been rejected."
            );
        }

        /// <summary>
        /// Confirms optional media can be null without removing an entry.
        /// Cry references are shared metadata, so change both counterparts.
        /// </summary>
        private static void RunMissingMediaCheck(
            JObject original,
            string directory
        )
        {
            JObject copy = (JObject)original.DeepClone();
            JObject first = FirstEntry(copy);

            string firstId = first.Value<string>("id");

            string counterpartId =
                first.Value<string>("formId") + ":" +
                first.Value<string>("gender") + ":" +
                (first.Value<bool>("isShiny") ? "normal" : "shiny");

            JObject counterpart = FindEntry(copy, counterpartId);

            ClearMedia(first);
            ClearMedia(counterpart);

            string path = WriteTemporaryDocument(
                directory,
                "missing-media",
                copy
            );

            CatalogueDocument loaded = CatalogueLoader.Load(path);

            if (loaded.Entries.Count != ArrayProperty(original, "entries").Count)
            {
                throw new InvalidOperationException(
                    "Missing media changed the number of collection entries."
                );
            }

            bool foundFirst = false;
            bool foundCounterpart = false;

            foreach (CatalogueEntry entry in loaded.Entries)
            {
                if (entry.Id != firstId && entry.Id != counterpartId)
                {
                    continue;
                }

                if (
                    entry.Sprite.Url != null ||
                    entry.Sprite.FallbackUrl != null ||
                    entry.Cries.Latest != null ||
                    entry.Cries.Legacy != null
                )
                {
                    throw new InvalidOperationException(
                        "Missing media did not remain null after loading."
                    );
                }

                foundFirst |= entry.Id == firstId;
                foundCounterpart |= entry.Id == counterpartId;
            }

            if (!foundFirst || !foundCounterpart)
            {
                throw new InvalidOperationException(
                    "An entry with missing media was lost during loading."
                );
            }

            Console.WriteLine(
                "PASS: Missing sprite and cry references were accepted " +
                "without removing either counterpart."
            );
        }

        private static void ClearMedia(JObject entry)
        {
            entry["sprite"] = new JObject
            {
                ["url"] = JValue.CreateNull(),
                ["fallbackUrl"] = JValue.CreateNull()
            };

            entry["cries"] = new JObject
            {
                ["latest"] = JValue.CreateNull(),
                ["legacy"] = JValue.CreateNull()
            };
        }

        private static string WriteTemporaryDocument(
            string directory,
            string filename,
            JObject document
        )
        {
            string path = Path.Combine(directory, filename + ".json");

            File.WriteAllText(
                path,
                document.ToString(Formatting.Indented)
            );

            return path;
        }

        private static JArray ArrayProperty(JObject document, string name)
        {
            JArray array = document[name] as JArray;

            if (array == null)
            {
                throw new InvalidOperationException(
                    "Test fixture has no array named " + name + "."
                );
            }

            return array;
        }

        private static JObject FirstEntry(JObject document)
        {
            return FirstObject(ArrayProperty(document, "entries"));
        }

        private static JObject FirstGroup(JObject document)
        {
            return FirstObject(ArrayProperty(document, "encounterGroups"));
        }

        private static JObject FirstObject(JArray array)
        {
            if (array.Count == 0)
            {
                throw new InvalidOperationException(
                    "Test fixture contains an empty array."
                );
            }

            JObject result = array[0] as JObject;

            if (result == null)
            {
                throw new InvalidOperationException(
                    "Test fixture expected a JSON object."
                );
            }

            return result;
        }

        private static JObject FindEntry(JObject document, string id)
        {
            foreach (JToken token in ArrayProperty(document, "entries"))
            {
                JObject entry = token as JObject;

                if (entry != null && entry.Value<string>("id") == id)
                {
                    return entry;
                }
            }

            throw new InvalidOperationException(
                "Test fixture could not find entry " + id + "."
            );
        }
    }
}