﻿using com.csutil.model.immutable;
using com.csutil.ui;
using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace com.csutil.tests.ui {

    public class Ui10_AppFlowTrackingTests {
        [UnityTest]
        public IEnumerator Ui10_AppFlowTracking() { yield return UnitTestMono.LoadAndRunUiTest("Ui10_Screen1"); }
    }

    public class Ui10_AppFlowTracking : UnitTestMono {

        public override IEnumerator RunTest() {

            var testTracker = new TestAppFlowTracker();
            AppFlow.instance = testTracker;
            AppFlow.instance.ActivateLinkMapTracking();
            AppFlow.instance.ActivatePrefabLoadTracking();
            AppFlow.instance.ActivateUiEventTracking();
            AppFlow.instance.ActivateViewStackTracking();

            setupImmutableDatastore();
            // In setupImmutableDatastore() Log.MethodEntered is used, so there must be a recorded method:
            Assert.AreEqual(1, testTracker.recordedEvents.Count(x => x.category == AppFlow.catMethod));
            // In setupImmutableDatastore() the datastore will be set as a singleton:
            Assert.AreEqual(1, testTracker.recordedEvents.Count(x => x.action.Contains("DataStore")));

            var store = MyDataModel.GetStore();
            Assert.NotNull(store);
            store.Dispatch(new ActionSetBool1() { newB = true });
            store.Dispatch(new ActionSetString1 { newS = "abc" });
            Assert.AreEqual(true, store.GetState().subSection1.bool1);
            Assert.AreEqual("abc", store.GetState().subSection1.string1);
            // After the 2 mutations, there should be 2 mutation AppFlow events recorded:
            Assert.AreEqual(2, testTracker.recordedEvents.Count(x => x.category == AppFlow.catMutation));

            var presenter = new MyDataModelPresenter();
            presenter.targetView = gameObject;
            yield return presenter.LoadModelIntoView(store).AsCoroutine();
            // After the presenter loaded the UI there should be a load start and load end event recorded:
            Assert.AreEqual(2, testTracker.recordedEvents.Count(x => x.category == AppFlow.catPresenter));
            // The MyDataModelPresenter uses a GetLinkMap() when connecting to the view:
            Assert.AreEqual(1, testTracker.recordedEvents.Count(x => x.category == AppFlow.catLinked));

            yield return new WaitForSeconds(1);

        }

        class TestAppFlowTracker : IAppFlow {
            public List<AppFlowEvent> recordedEvents = new List<AppFlowEvent>();
            public void TrackEvent(string category, string action, params object[] args) {
                var e = new AppFlowEvent() { category = category, action = action };
                Log.d("new AppFlowEvent: " + e);
                recordedEvents.Add(e);
            }
        }

        class AppFlowEvent {
            public string category;
            public string action;
            public override string ToString() { return category + " - " + action; }
        }

        void setupImmutableDatastore() {
            Log.MethodEntered();
            var log = Middlewares.NewAppFlowTrackerMiddleware<MyDataModel>();
            var store = new DataStore<MyDataModel>(MainReducer, new MyDataModel(new MyDataModel.SubSection1(string1: "", bool1: false)), log);
            IoC.inject.SetSingleton<IDataStore<MyDataModel>>(store);
        }

        class MyDataModel {

            public static IDataStore<MyDataModel> GetStore() { return IoC.inject.Get<IDataStore<MyDataModel>>(null); }

            public readonly SubSection1 subSection1;
            public MyDataModel(SubSection1 subSection1) { this.subSection1 = subSection1; }

            public class SubSection1 {
                public readonly bool bool1;
                public readonly string string1;
                public SubSection1(string string1, bool bool1) {
                    this.string1 = string1;
                    this.bool1 = bool1;
                }
            }
        }

        class MyDataModelPresenter : Presenter<IDataStore<MyDataModel>> {

            public GameObject targetView { get; set; }

            public Task OnLoad(IDataStore<MyDataModel> store) {
                var map = targetView.GetLinkMap();

                var string1 = map.Get<InputField>("string1");
                string1.SubscribeToStateChanges(store, state => state.subSection1.string1, newVal => string1.text = newVal);
                string1.SetOnValueChangedActionThrottled((newVal) => {
                    store.Dispatch(new ActionSetString1() { newS = newVal });
                });

                var bool1 = map.Get<Toggle>("bool1");
                bool1.SubscribeToStateChanges(store, state => state.subSection1.bool1, newVal => bool1.isOn = newVal);
                bool1.SetOnValueChangedAction((newVal) => {
                    store.Dispatch(new ActionSetBool1() { newB = newVal });
                    return true;
                });

                map.Get<Button>("ToggleBool1").SetOnClickAction(delegate {
                    store.Dispatch(new ActionSetBool1() { newB = !store.GetState().subSection1.bool1 });
                });
                map.Get<Button>("SetString1").SetOnClickAction(delegate {
                    store.Dispatch(new ActionSetString1() { newS = "abc" });
                });
                map.Get<Button>("ShowDialog").SetOnClickAction(async (b) => {
                    var dialog = await ConfirmCancelDialog.Show("I am a dialog", "Current model.string1=" + store.GetState().subSection1.string1);
                    Log.d("Dialog was confirmed: " + dialog.dialogWasConfirmed);
                });
                return Task.CompletedTask;
            }

        }

        class ActionSetString1 { public string newS; }
        class ActionSetBool1 { public bool newB; }

        MyDataModel MainReducer(MyDataModel prev, object action) {
            bool changed = false;
            var subSection1 = prev.subSection1.Mutate(action, MyDataModelSubSection1Reducer, ref changed);
            if (changed) { return new MyDataModel(subSection1); }
            return prev;
        }

        MyDataModel.SubSection1 MyDataModelSubSection1Reducer(MyDataModel.SubSection1 prev, object action) {
            if (action is ActionSetBool1 a1) { return new MyDataModel.SubSection1(prev.string1, a1.newB); }
            if (action is ActionSetString1 a2) { return new MyDataModel.SubSection1(a2.newS, prev.bool1); }
            return prev;
        }

    }

}