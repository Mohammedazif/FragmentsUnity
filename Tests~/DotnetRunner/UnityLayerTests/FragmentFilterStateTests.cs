using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using UnityEngine;

namespace FragmentsUnity.Tests
{
    [TestFixture]
    public sealed class FragmentFilterStateTests
    {
        private const string StoreyCategory = "IFCBUILDINGSTOREY";
        private const string WallCategory = "IfcWall";
        private const string WindowCategory = "IfcWindow";
        private const string DoorCategory = "IfcDoor";
        private const string LevelOne = "Level 1";
        private const string LevelTwo = "Level 2";
        private const string FireRatingName = "FireRating";
        private const string FireRatingValue = "REI 60";

        private const int LevelOneId = 1;
        private const int LevelTwoId = 2;
        private const int FirstWallId = 10;
        private const int SecondWallId = 11;
        private const int ThirdWallId = 12;
        private const int UpperWallId = 13;
        private const int DoorId = 20;
        private const int FirstWindowId = 30;
        private const int SecondWindowId = 31;

        private const int LevelOneElementCount = 4;
        private const int LevelTwoElementCount = 3;
        private const int WallCount = 4;

        private static readonly int[] AllLocalIds =
        {
            LevelOneId, LevelTwoId, FirstWallId, SecondWallId, ThirdWallId, DoorId, UpperWallId,
            FirstWindowId, SecondWindowId
        };

        private GameObject _root;
        private FragmentModel _model;
        private FragmentFilterState _state;

        [SetUp]
        public void SetUp()
        {
            Debug.capturedWarnings.Clear();

            _root = new GameObject("Model");
            _model = _root.AddComponent<FragmentModel>();
            _model.SetAsset(FragmentModelAsset.Create(FragmentModelFixture.ResultWith(BuildItems())));

            _state = new FragmentFilterState();
            _state.SetModel(_model);
        }

        [Test]
        public void Storeys_AreListedBiggestFirst()
        {
            Assert.That(RowNames(_state.Storeys), Is.EqualTo(new[] { LevelOne, LevelTwo }));
            Assert.That(_state.Storeys[0].Count, Is.EqualTo(LevelOneElementCount));
            Assert.That(_state.Storeys[1].Count, Is.EqualTo(LevelTwoElementCount));
        }

        [Test]
        public void Categories_AreListedBiggestFirstThenAlphabetically()
        {
            Assert.That(
                RowNames(_state.Categories),
                Is.EqualTo(new[] { WallCategory, StoreyCategory, WindowCategory, DoorCategory }));
            Assert.That(_state.Categories[0].Count, Is.EqualTo(WallCount));
        }

        [Test]
        public void StoreyRow_CountsItsContentsButAlsoActsOnTheStoreyItself()
        {
            FragmentFilterRow levelOne = _state.Storeys[0];

            Assert.That(levelOne.Count, Is.EqualTo(LevelOneElementCount));
            Assert.That(
                levelOne.LocalIds,
                Is.EqualTo(new[] { LevelOneId, FirstWallId, SecondWallId, ThirdWallId, DoorId }));
        }

        [Test]
        public void IsolateCategory_ReturnsThatCategorysElements()
        {
            IReadOnlyList<int> localIds = _state.IsolateCategory(WallCategory);

            Assert.That(localIds, Is.EqualTo(new[] { FirstWallId, SecondWallId, ThirdWallId, UpperWallId }));
            Assert.That(_state.IsolatedCategory, Is.EqualTo(WallCategory));
            Assert.That(_state.IsolatedStorey, Is.Null);
        }

        [Test]
        public void IsolateCategory_LeavesEveryOtherRowHidden()
        {
            _state.IsolateCategory(WallCategory);

            Assert.That(Row(_state.Categories, WallCategory).IsVisible, Is.True);
            Assert.That(Row(_state.Categories, DoorCategory).IsVisible, Is.False);
            Assert.That(Row(_state.Storeys, LevelOne).IsVisible, Is.False);
        }

        [Test]
        public void IsolateStorey_ReturnsTheStoreyAndEverythingInIt()
        {
            IReadOnlyList<int> localIds = _state.IsolateStorey(LevelOne);

            Assert.That(localIds, Is.EqualTo(new[] { LevelOneId, FirstWallId, SecondWallId, ThirdWallId, DoorId }));
            Assert.That(_state.IsolatedStorey, Is.EqualTo(LevelOne));
            Assert.That(_state.IsolatedCategory, Is.Null);
        }

        [Test]
        public void IsolateStorey_HidesACategoryThatReachesIntoAnotherStorey()
        {
            _state.IsolateStorey(LevelOne);

            Assert.That(Row(_state.Storeys, LevelOne).IsVisible, Is.True);
            Assert.That(Row(_state.Storeys, LevelTwo).IsVisible, Is.False);
            Assert.That(Row(_state.Categories, DoorCategory).IsVisible, Is.True);
            Assert.That(Row(_state.Categories, WallCategory).IsVisible, Is.False);
        }

        [Test]
        public void IsolateStorey_UnknownName_HidesTheWholeModel()
        {
            IReadOnlyList<int> localIds = _state.IsolateStorey("Roof");

            Assert.That(localIds, Is.Empty);
            Assert.That(_state.HiddenCount, Is.EqualTo(AllLocalIds.Length));
        }

        [Test]
        public void IsolateAttribute_ReturnsTheElementsCarryingTheValue()
        {
            IReadOnlyList<int> localIds = _state.IsolateAttribute(FireRatingName, FireRatingValue, true);

            Assert.That(localIds, Is.EqualTo(new[] { FirstWallId, DoorId }));
            Assert.That(Row(_state.Categories, WindowCategory).IsVisible, Is.False);
        }

        [Test]
        public void IsolateAttribute_WithoutAName_ChangesNothing()
        {
            IReadOnlyList<int> localIds = _state.IsolateAttribute(string.Empty, FireRatingValue, true);

            Assert.That(localIds, Is.Empty);
            Assert.That(_state.HiddenCount, Is.Zero);
        }

        [Test]
        public void CanIsolateAttribute_WithoutANameOrWithoutAModel_IsFalse()
        {
            Assert.Multiple(() =>
            {
                Assert.That(_state.CanIsolateAttribute(FireRatingName), Is.True);
                Assert.That(_state.CanIsolateAttribute(string.Empty), Is.False);
                Assert.That(_state.CanIsolateAttribute(null), Is.False);
            });

            _state.SetModel(null);

            Assert.That(_state.CanIsolateAttribute(FireRatingName), Is.False);
        }

        [Test]
        public void ToggleCategory_Off_HidesOnlyThatCategory()
        {
            IReadOnlyList<int> localIds = _state.ToggleCategory(WindowCategory, false);

            Assert.That(localIds, Is.EqualTo(new[] { FirstWindowId, SecondWindowId }));
            Assert.That(Row(_state.Categories, WindowCategory).IsVisible, Is.False);
            Assert.That(Row(_state.Categories, WallCategory).IsVisible, Is.True);
        }

        [Test]
        public void ToggleCategory_Off_AlsoUnchecksTheStoreyItReachesInto()
        {
            _state.ToggleCategory(WindowCategory, false);

            Assert.That(Row(_state.Storeys, LevelTwo).IsVisible, Is.False);
            Assert.That(Row(_state.Storeys, LevelOne).IsVisible, Is.True);
        }

        [Test]
        public void ToggleCategory_BackOn_ShowsEveryRowAgain()
        {
            _state.ToggleCategory(WindowCategory, false);
            _state.ToggleCategory(WindowCategory, true);

            Assert.That(Row(_state.Categories, WindowCategory).IsVisible, Is.True);
            Assert.That(_state.HiddenCount, Is.Zero);
        }

        [Test]
        public void ToggleStorey_Off_DropsAnyIsolation()
        {
            _state.IsolateCategory(WallCategory);
            _state.ToggleStorey(LevelTwo, false);

            Assert.That(_state.IsolatedCategory, Is.Null);
            Assert.That(_state.IsolatedStorey, Is.Null);
        }

        [Test]
        public void ClearAll_ShowsEveryRowAgain()
        {
            _state.IsolateStorey(LevelOne);
            _state.ClearAll();

            Assert.That(_state.HiddenCount, Is.Zero);
            Assert.That(_state.IsolatedStorey, Is.Null);
            foreach (FragmentFilterRow row in _state.Categories)
            {
                Assert.That(row.IsVisible, Is.True, row.Name);
            }
            foreach (FragmentFilterRow row in _state.Storeys)
            {
                Assert.That(row.IsVisible, Is.True, row.Name);
            }
        }

        [Test]
        public void StatusMessage_ReportsHowManyElementsAreHidden()
        {
            _state.ToggleCategory(WindowCategory, false);

            Assert.That(_state.StatusMessage, Does.Contain("2"));
        }

        [Test]
        public void StatusMessage_WithNothingHidden_SaysTheWholeModelIsShowing()
        {
            Assert.That(_state.StatusMessage, Is.EqualTo("Showing the whole model."));
        }

        [Test]
        public void WithoutAModel_ThereAreNoRowsToList()
        {
            _state.SetModel(null);

            Assert.That(_state.HasModel, Is.False);
            Assert.That(_state.Storeys, Is.Empty);
            Assert.That(_state.Categories, Is.Empty);
            Assert.That(_state.WarningMessage, Is.Null);
            Assert.That(_state.StatusMessage, Does.Contain("Select a Fragments model"));
        }

        [Test]
        public void SetModel_WithTheSameModel_KeepsTheCurrentFilter()
        {
            _state.ToggleCategory(WindowCategory, false);
            _state.SetModel(_model);

            Assert.That(Row(_state.Categories, WindowCategory).IsVisible, Is.False);
        }

        [Test]
        public void WithoutAVisibilityIndex_TheWarningAsksForAHierarchyImport()
        {
            Assert.That(_state.SupportsFiltering, Is.False);
            Assert.That(_state.SupportsElementFiltering, Is.False);
            Assert.That(_state.WarningMessage, Does.Contain("hierarchy modes"));
        }

        [Test]
        public void OnAnElementGranularScene_ThereIsNothingToWarnAbout()
        {
            AddVisibilityIndex(true);

            Assert.That(_state.SupportsFiltering, Is.True);
            Assert.That(_state.SupportsElementFiltering, Is.True);
            Assert.That(_state.WarningMessage, Is.Null);
        }

        [Test]
        public void OnAMergedScene_TheWarningSaysWholeChunksHide()
        {
            AddVisibilityIndex(false);

            Assert.That(_state.SupportsFiltering, Is.True);
            Assert.That(_state.SupportsElementFiltering, Is.False);
            Assert.That(_state.WarningMessage, Does.Contain("hide whole chunks"));
        }

        [Test]
        public void Refresh_ReadsBackAFilterAppliedOutsideTheWindow()
        {
            AddVisibilityIndex(true);
            _root.AddComponent<FragmentFilter>().IsolateByCategory(WallCategory);

            _state.Refresh();

            Assert.That(Row(_state.Categories, WallCategory).IsVisible, Is.True);
            Assert.That(Row(_state.Categories, DoorCategory).IsVisible, Is.False);
            Assert.That(_state.HiddenCount, Is.EqualTo(AllLocalIds.Length - WallCount));
        }

        private FragmentVisibilityIndex AddVisibilityIndex(bool elementGranular)
        {
            FragmentVisibilityIndex index = _root.AddComponent<FragmentVisibilityIndex>();
            index.SetElementGranular(elementGranular);

            foreach (int localId in AllLocalIds)
            {
                var element = new GameObject(localId.ToString(CultureInfo.InvariantCulture));
                element.transform.SetParent(_root.transform, false);
                element.AddComponent<MeshRenderer>();
                index.Register(localId, element);
            }

            _state.Refresh();
            return index;
        }

        private static IEnumerable<FragmentItemMetadata> BuildItems()
        {
            var wall = Element(FirstWallId, WallCategory, "Wall A", LevelOne);
            wall.Attributes.Add(new FragmentAttribute(FireRatingName, FireRatingValue, "IfcLabel"));

            var door = Element(DoorId, DoorCategory, "Door", LevelOne);
            door.Attributes.Add(new FragmentAttribute(FireRatingName, FireRatingValue, "IfcLabel"));

            return new[]
            {
                Storey(LevelOneId, LevelOne),
                Storey(LevelTwoId, LevelTwo),
                wall,
                Element(SecondWallId, WallCategory, "Wall B", LevelOne),
                Element(ThirdWallId, WallCategory, "Wall C", LevelOne),
                door,
                Element(UpperWallId, WallCategory, "Wall D", LevelTwo),
                Element(FirstWindowId, WindowCategory, "Window A", LevelTwo),
                Element(SecondWindowId, WindowCategory, "Window B", LevelTwo)
            };
        }

        private static FragmentItemMetadata Storey(int localId, string name)
        {
            return new FragmentItemMetadata { LocalId = localId, Category = StoreyCategory, Name = name };
        }

        private static FragmentItemMetadata Element(int localId, string category, string name, string storeyName)
        {
            return new FragmentItemMetadata
            {
                LocalId = localId,
                Category = category,
                Name = name,
                StoreyName = storeyName
            };
        }

        private static IReadOnlyList<string> RowNames(IReadOnlyList<FragmentFilterRow> rows)
        {
            var names = new List<string>();
            foreach (FragmentFilterRow row in rows)
            {
                names.Add(row.Name);
            }
            return names;
        }

        private static FragmentFilterRow Row(IReadOnlyList<FragmentFilterRow> rows, string name)
        {
            foreach (FragmentFilterRow row in rows)
            {
                if (row.Name == name)
                {
                    return row;
                }
            }
            Assert.Fail($"No row named '{name}'.");
            return null;
        }
    }
}
