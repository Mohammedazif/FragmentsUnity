using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace FragmentsUnity.Tests
{
    [TestFixture]
    public sealed class FragmentFilterIntegrationTests
    {
        private const string VolumeCategory = "IFCSPACE";

        private const string JoysonVisibleStorey = "Level 1";
        private const string JoysonHiddenStorey = "First Floor Level";
        private const int JoysonVisibleStoreyElementCount = 887;
        private const int JoysonHiddenStoreyElementCount = 474;
        private const int JoysonVisibleStoreyRendererCount = 9945;
        private const int JoysonHiddenStoreyRendererCount = 2478;
        private const int JoysonVolumeRendererCount = 1;

        private const int JoysonElementCount = 1986;
        private const int JoysonOutOfStoreyElementCount = 1099;
        private const int JoysonMergedLeakedElementCount = 385;
        private const int OneWarning = 1;

        private const string Ar520Storey = "GF-GROUND FLOOR - FFL";
        private const int Ar520StoreyElementCount = 1113;
        private const string Ar520VisibleCategory = "IFCDOOR";
        private const string Ar520HiddenCategory = "IFCMEMBER";
        private const int Ar520VisibleCategoryElementCount = 6;
        private const int Ar520HiddenCategoryElementCount = 770;
        private const int Ar520VisibleCategoryRendererCount = 48;
        private const int Ar520HiddenCategoryRendererCount = 770;

        private const int NothingDisabled = 0;

        [Test]
        public void Build_JoysonHierarchyPerBody_DisablesOnlyTheVolumeCategoryRenderer()
        {
            FragmentSceneBuildResult scene = BuildJoyson();
            FragmentImportResult parsed = FragmentSampleModels.LoadOrIgnore(FragmentSampleModels.JoysonFileName);

            MeshRenderer[] disabled = DisabledRenderers(scene.Root);
            Assert.That(disabled, Has.Length.EqualTo(JoysonVolumeRendererCount));

            Assert.Multiple(() =>
            {
                Assert.That(parsed.FindItem(ElementIdOf(disabled[0])).Category, Is.EqualTo(VolumeCategory).IgnoreCase);
                Assert.That(DisabledColliders(scene.Root), Is.Empty);
            });
        }

        [Test]
        public void IsolateByStorey_JoysonHierarchyPerBody_KeepsThatStoreyDrawnAndDisablesAnotherStorey()
        {
            FragmentSceneBuildResult scene = BuildJoyson();
            var model = scene.Root.GetComponent<FragmentModel>();
            var index = scene.Root.GetComponent<FragmentVisibilityIndex>();

            List<int> visibleIds = model.FindByStorey(JoysonVisibleStorey);
            List<int> hiddenIds = model.FindByStorey(JoysonHiddenStorey);
            scene.Root.GetComponent<FragmentFilter>().IsolateByStorey(JoysonVisibleStorey);

            MeshRenderer[] visibleRenderers = RenderersOf(scene.Root, visibleIds);
            MeshRenderer[] hiddenRenderers = RenderersOf(scene.Root, hiddenIds);

            Assert.Multiple(() =>
            {
                Assert.That(visibleIds, Has.Count.EqualTo(JoysonVisibleStoreyElementCount));
                Assert.That(hiddenIds, Has.Count.EqualTo(JoysonHiddenStoreyElementCount));
                Assert.That(index.IsFilterActive, Is.True);
                Assert.That(visibleIds.Where(index.IsHidden), Is.Empty);
                Assert.That(hiddenIds.Where(localId => !index.IsHidden(localId)), Is.Empty);

                Assert.That(visibleRenderers, Has.Length.EqualTo(JoysonVisibleStoreyRendererCount));
                Assert.That(visibleRenderers.Where(renderer => renderer.forceRenderingOff), Is.Empty);
                Assert.That(CollidersOf(scene.Root, visibleIds).Where(collider => !collider.enabled), Is.Empty);

                Assert.That(hiddenRenderers, Has.Length.EqualTo(JoysonHiddenStoreyRendererCount));
                Assert.That(hiddenRenderers.Where(renderer => !renderer.forceRenderingOff), Is.Empty);
                Assert.That(CollidersOf(scene.Root, hiddenIds).Where(collider => collider.enabled), Is.Empty);
            });
        }

        [Test]
        public void ClearFilter_JoysonHierarchyPerBody_RestoresEverythingExceptTheVolumeTheBuildHadAlreadyHidden()
        {
            FragmentSceneBuildResult scene = BuildJoyson();
            MeshRenderer[] disabledByTheBuild = DisabledRenderers(scene.Root);
            var filter = scene.Root.GetComponent<FragmentFilter>();

            filter.IsolateByStorey(JoysonVisibleStorey);
            filter.ClearFilter();

            Assert.Multiple(() =>
            {
                Assert.That(disabledByTheBuild, Has.Length.EqualTo(JoysonVolumeRendererCount));
                Assert.That(DisabledRenderers(scene.Root), Is.EqualTo(disabledByTheBuild));
                Assert.That(HiddenRenderers(scene.Root), Is.Empty);
                Assert.That(DisabledColliders(scene.Root), Is.Empty);
                Assert.That(scene.Root.GetComponent<FragmentVisibilityIndex>().IsFilterActive, Is.False);
            });
        }

        // Pins the known merged-mode leak: a chunk is the smallest unit that can hide, so out-of-storey parts of a kept chunk stay drawn.
        [Test]
        public void IsolateByStorey_JoysonMergedWholeModel_RecordsEveryOtherElementHiddenYetStillDrawsSomeOfThem()
        {
            FragmentSceneBuildResult scene = FragmentSampleModels.BuildAsImportedOrIgnore(
                FragmentSampleModels.JoysonFileName, FragmentImportMode.MergedWholeModel);
            var model = scene.Root.GetComponent<FragmentModel>();
            var index = scene.Root.GetComponent<FragmentVisibilityIndex>();

            var inStorey = new HashSet<int>(model.FindByStorey(JoysonVisibleStorey));
            int[] outOfStorey = model.Data.Items
                .Select(item => item.LocalId)
                .Where(localId => !inStorey.Contains(localId))
                .ToArray();

            Debug.capturedWarnings.Clear();
            scene.Root.GetComponent<FragmentFilter>().IsolateByStorey(JoysonVisibleStorey);
            HashSet<int> stillDrawn = DrawnLocalIds(scene.Root);

            Assert.Multiple(() =>
            {
                Assert.That(index.SupportsElementFiltering, Is.False);
                Assert.That(Debug.capturedWarnings, Has.Count.EqualTo(OneWarning));

                Assert.That(model.Data.Items, Has.Count.EqualTo(JoysonElementCount));
                Assert.That(outOfStorey, Has.Length.EqualTo(JoysonOutOfStoreyElementCount));
                Assert.That(outOfStorey.Where(localId => !index.IsHidden(localId)), Is.Empty);

                Assert.That(outOfStorey.Count(stillDrawn.Contains),
                    Is.EqualTo(JoysonMergedLeakedElementCount));
                Assert.That(inStorey.Count(stillDrawn.Contains),
                    Is.EqualTo(JoysonVisibleStoreyElementCount));
            });
        }

        [Test]
        public void IsolateByCategory_Ar520HierarchyPerBody_KeepsThatCategoryDrawnAndDisablesAnother()
        {
            FragmentSceneBuildResult scene = BuildAr520();
            var model = scene.Root.GetComponent<FragmentModel>();
            var index = scene.Root.GetComponent<FragmentVisibilityIndex>();

            List<int> visibleIds = model.FindByCategory(Ar520VisibleCategory);
            List<int> hiddenIds = model.FindByCategory(Ar520HiddenCategory);
            scene.Root.GetComponent<FragmentFilter>().IsolateByCategory(Ar520VisibleCategory);

            MeshRenderer[] visibleRenderers = RenderersOf(scene.Root, visibleIds);
            MeshRenderer[] hiddenRenderers = RenderersOf(scene.Root, hiddenIds);

            Assert.Multiple(() =>
            {
                Assert.That(visibleIds, Has.Count.EqualTo(Ar520VisibleCategoryElementCount));
                Assert.That(hiddenIds, Has.Count.EqualTo(Ar520HiddenCategoryElementCount));
                Assert.That(index.IsFilterActive, Is.True);

                Assert.That(visibleRenderers, Has.Length.EqualTo(Ar520VisibleCategoryRendererCount));
                Assert.That(visibleRenderers.Where(renderer => renderer.forceRenderingOff), Is.Empty);
                Assert.That(CollidersOf(scene.Root, visibleIds).Where(collider => !collider.enabled), Is.Empty);

                Assert.That(hiddenRenderers, Has.Length.EqualTo(Ar520HiddenCategoryRendererCount));
                Assert.That(hiddenRenderers.Where(renderer => !renderer.forceRenderingOff), Is.Empty);
                Assert.That(CollidersOf(scene.Root, hiddenIds).Where(collider => collider.enabled), Is.Empty);
            });
        }

        [Test]
        public void IsolateByStorey_Ar520HierarchyPerBody_KeepsItsOnlyStoreyEntirelyDrawn()
        {
            FragmentSceneBuildResult scene = BuildAr520();
            var model = scene.Root.GetComponent<FragmentModel>();

            List<int> visibleIds = model.FindByStorey(Ar520Storey);
            scene.Root.GetComponent<FragmentFilter>().IsolateByStorey(Ar520Storey);

            Assert.Multiple(() =>
            {
                Assert.That(visibleIds, Has.Count.EqualTo(Ar520StoreyElementCount));
                Assert.That(HiddenRenderers(scene.Root), Is.Empty);
                Assert.That(DisabledColliders(scene.Root), Is.Empty);
            });
        }

        [Test]
        public void ClearFilter_Ar520HierarchyPerBody_RestoresEveryRendererBecauseNoVolumeCategoryWasImported()
        {
            FragmentSceneBuildResult scene = BuildAr520();
            var filter = scene.Root.GetComponent<FragmentFilter>();

            filter.IsolateByCategory(Ar520VisibleCategory);
            filter.ClearFilter();

            Assert.Multiple(() =>
            {
                Assert.That(DisabledRenderers(scene.Root), Has.Length.EqualTo(NothingDisabled));
                Assert.That(HiddenRenderers(scene.Root), Has.Length.EqualTo(NothingDisabled));
                Assert.That(DisabledColliders(scene.Root), Has.Length.EqualTo(NothingDisabled));
                Assert.That(scene.Root.GetComponent<FragmentVisibilityIndex>().IsFilterActive, Is.False);
            });
        }

        private static FragmentSceneBuildResult BuildAr520()
        {
            return FragmentSampleModels.BuildAsImportedOrIgnore(
                FragmentSampleModels.Ar520FileName, FragmentImportMode.HierarchyPerBody);
        }

        private static FragmentSceneBuildResult BuildJoyson()
        {
            return FragmentSampleModels.BuildAsImportedOrIgnore(
                FragmentSampleModels.JoysonFileName, FragmentImportMode.HierarchyPerBody);
        }

        private static int ElementIdOf(Component drawable)
        {
            return drawable.gameObject.GetComponent<FragmentElementReference>().LocalId;
        }

        private static HashSet<int> DrawnLocalIds(GameObject root)
        {
            var drawn = new HashSet<int>();
            foreach (FragmentElementTable table in root.GetComponentsInChildren<FragmentElementTable>(true))
            {
                var renderer = table.gameObject.GetComponent<MeshRenderer>();
                if (renderer == null || !renderer.enabled || renderer.forceRenderingOff)
                {
                    continue;
                }

                int triangles = FragmentSampleModels.CountTriangles(table.gameObject);
                for (int triangle = 0; triangle < triangles; triangle++)
                {
                    int localId = table.FindLocalId(triangle);
                    if (localId >= 0)
                    {
                        drawn.Add(localId);
                    }
                }
            }
            return drawn;
        }

        private static MeshRenderer[] DisabledRenderers(GameObject root)
        {
            return root.GetComponentsInChildren<MeshRenderer>(true)
                .Where(renderer => !renderer.enabled)
                .ToArray();
        }

        private static MeshRenderer[] HiddenRenderers(GameObject root)
        {
            return root.GetComponentsInChildren<MeshRenderer>(true)
                .Where(renderer => renderer.forceRenderingOff)
                .ToArray();
        }

        private static MeshCollider[] DisabledColliders(GameObject root)
        {
            return root.GetComponentsInChildren<MeshCollider>(true)
                .Where(collider => !collider.enabled)
                .ToArray();
        }

        private static MeshRenderer[] RenderersOf(GameObject root, List<int> localIds)
        {
            return DrawableObjects(root, localIds)
                .Select(target => target.GetComponent<MeshRenderer>())
                .Where(renderer => renderer != null)
                .ToArray();
        }

        private static MeshCollider[] CollidersOf(GameObject root, List<int> localIds)
        {
            return DrawableObjects(root, localIds)
                .Select(target => target.GetComponent<MeshCollider>())
                .Where(collider => collider != null)
                .ToArray();
        }

        private static IEnumerable<GameObject> DrawableObjects(GameObject root, List<int> localIds)
        {
            var wanted = new HashSet<int>(localIds);
            return root.GetComponentsInChildren<FragmentElementReference>(true)
                .Where(reference => wanted.Contains(reference.LocalId))
                .Select(reference => reference.gameObject);
        }
    }
}
