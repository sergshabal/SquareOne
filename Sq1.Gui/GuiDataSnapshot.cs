﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.Serialization;

using Newtonsoft.Json;
using Sq1.Gui.Forms;
using Sq1.Widgets;

//using Newtonsoft.Json;
//	[DataContract]
//		[JsonIgnore]
//		[DataMember]

namespace Sq1.Gui {
	//[DataContract]
	public class GuiDataSnapshot {
		[JsonIgnore]	public Dictionary<int, ChartFormManager> ChartFormManagers;
		[JsonProperty]	public Point MainFormLocation;
		[JsonProperty]	public Size MainFormSize;
		[JsonProperty]	public bool MainFormIsFullScreen;
		[JsonProperty]	public int ChartSernoLastUsed;
		[JsonIgnore]	public int ChartSernoNextAvailable { get { return ++this.ChartSernoLastUsed; } }
		[JsonProperty]	public int ChartSernoLastKnownHadFocus;
		[JsonIgnore]	public string ChartSernosInstantiatedAsString { get {
				string ret = "";
				foreach (int chartSerno in this.ChartFormManagers.Keys) ret += chartSerno + ",";
				ret = ret.TrimEnd(",".ToCharArray());
				return ret;
			} }

		public GuiDataSnapshot() {
			this.ChartFormManagers = new Dictionary<int, ChartFormManager>();
		}

		//public void RebuildDeserializedChartFormsManagers(MainForm mainForm) {
		//	foreach (int serno in this.ChartFormsManagers.Keys) {
		//		if (serno > ChartSernoLastUsed) ChartSernoLastUsed = serno;
		//	}

		//	foreach (int serno in this.ChartFormsManagers.Keys) {
		//		ChartFormsManager mgr = this.ChartFormsManagers[serno];
		//		//mgr.ChartSerno = serno;
		//		mgr.RebuildAfterDeserialization(mainForm);
		//	}
		//}
		public void AddChartFormsManagerJustDeserialized(ChartFormManager mgr) {
			if (mgr.DataSnapshot.ChartSerno > this.ChartSernoLastUsed) this.ChartSernoLastUsed = mgr.DataSnapshot.ChartSerno;
			this.ChartFormManagers.Add(mgr.DataSnapshot.ChartSerno, mgr);
		}
		public ChartFormManager FindChartFormsManagerBySerno(int chartSerno, string invokerMsig = "CALLER_UNKNOWN", bool throwIfNotFound = true) {
			ChartFormManager ret = null;
			if (this.ChartFormManagers.ContainsKey(chartSerno) == false) {
				if (throwIfNotFound) {
					string msg = "CANT_DESERIALIZE_REPORTER/EDITOR_PARENT_CHART_NOT_FOUND"
						+ " CHART_DESERIALIZATION_FAILED_OR_CHARTFORM_DOESNT_EXIST_IN_LAYOUT.XML"
						+ " for chartSerno[" + chartSerno + "] ChartSernosInstantiated[" + this.ChartSernosInstantiatedAsString + "]";
					throw new Exception(msg + " " + invokerMsig);
				}
				return ret;
			}
			ret = this.ChartFormManagers[chartSerno];
			return ret;
		}
	}
}
