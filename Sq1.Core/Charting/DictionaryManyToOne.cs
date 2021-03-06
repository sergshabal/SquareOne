﻿using System;
using System.Collections.Generic;

namespace Sq1.Core.Charting {
	public class DictionaryManyToOne<CHART, ALERTS> {
		Dictionary<CHART, List<ALERTS>> Lookup;
		Dictionary<ALERTS, CHART> Reverse;
		
		public DictionaryManyToOne() {
			this.Lookup = new Dictionary<CHART, List<ALERTS>>();
			this.Reverse = new Dictionary<ALERTS, CHART>();
		}
		
		public void Register(CHART chart) {
			string msig = " //DictionaryManyToOne::Register()";
			if (this.Lookup.ContainsKey(chart)) {
				string msg = "ALREADY_REGISTERED_IN_this.Lookup chart[" + chart + "]";
				throw new Exception(msg + msig);
			}
//			if (this.Reverse.ContainsValue(chart)) {
//				string msg = "chart[" + chart + "] is already registered in this.Reverse";
//				throw new Exception(msg + msig);
//			}
			this.Lookup.Add(chart, new List<ALERTS>());
		}
		
		public void UnRegister(CHART chart) {
			string msig = " //DictionaryManyToOne::UnRegister()";
			if (this.Lookup.ContainsKey(chart) == false) {
				string msg = "NEVER_REGISTERED_IN_this.Lookup chart[" + chart + "]";
				throw new Exception(msg + msig);
			}
//			if (this.Reverse.ContainsValue(chart)) {
//				string msg = "chart[" + chart + "] was never registered in this.Reverse";
//				throw new Exception(msg + msig);
//			}
			this.Lookup.Add(chart, new List<ALERTS>());
		}
		
		public void ClearDependantsFor(CHART chart) {
			string msig = " //DictionaryManyToOne::ClearDependantsFor()";
			if (this.Lookup.ContainsKey(chart) == false) {
				string msg = "NEVER_REGISTERED_IN_this.Lookup chart[" + chart + "]";
				throw new Exception(msg + msig);
			}
			foreach (ALERTS dependant in this.Lookup[chart]) {
				if (this.Reverse.ContainsKey(dependant) == false) {
					string msg = "position[" + dependant + "] is not found in this.Reverse, you should've gotten an exception during Add()? AlmostImpossible happened";
					throw new Exception(msg + msig);
				}
				this.Reverse.Remove(dependant);
			}
			this.Lookup[chart] = new List<ALERTS>();
		}
		
		public void Add(CHART chart, ALERTS alert) {
			string msig = " //DictionaryManyToOne::Add(chart[" + chart + "], alert[" + alert + "])";
			if (this.Lookup.ContainsKey(chart) == false) {
				string msg = "NEVER_REGISTERED_IN_this.Lookup chart[" + chart + "]";
				throw new Exception(msg + msig);
			}
			if (this.Lookup[chart].Contains(alert)) {
				string msg = "ALREADY_ADDED_INTO_this.Lookup[chart] alert[" + alert + "]";
				throw new Exception(msg + msig);
			}
			if (this.Reverse.ContainsKey(alert)) {
				string msg = "ALREADY_ADDED_INTO_this.Reverse alert[" + alert + "]";
				throw new Exception(msg + msig);
			}
			this.Lookup[chart].Add(alert);
			this.Reverse.Add(alert, chart);
		}
		
//		public void AddRange(CHART chart, List<ALERTS> alerts) {
//			foreach (ALERTS alert in alerts) {
//				this.Add(chart, alert);
//			}
//		}

		public void Remove(CHART chart, ALERTS alert) {
			string msig = " //DictionaryManyToOne::Remove(chart[" + chart + "], alert[" + alert + "])";
			if (this.Lookup.ContainsKey(chart) == false) {
				string msg = "CONTAINER_WAS_NEVER_REGISTERED_IN_this.Lookup chart[" + chart + "]";
				throw new Exception(msg + msig);
			}
			if (this.Lookup[chart].Contains(alert) == false) {
				string msg = "LOOKUP_REFERENCE_WAS_NEVER_ADDED_FOR alert[" + alert + "]";
				throw new Exception(msg + msig);
			}
			if (this.Reverse.ContainsKey(alert) == false) {
				string msg = "REVERSE_REFERENCE_WAS_NEVER_ADDED_FOR alert[" + alert + "]";
				throw new Exception(msg + msig);
			}
			this.Lookup[chart].Remove(alert);
			this.Reverse.Remove(alert);
		}
		
		public bool IsItemRegisteredForAnyContainer(ALERTS alert) {
			string msig = " DictionaryManyToOne::IsItemRegistered(alert[" + alert + "]): ";
			return this.Reverse.ContainsKey(alert);
		}
		public CHART FindContainerFor(ALERTS alert) {
			string msig = " //DictionaryManyToOne::FindContainerFor(alert[" + alert + "])";
			if (this.Reverse.ContainsKey(alert) == false) {
				string msg = "REVERSE_REFERENCE_WAS_NEVER_ADDED_FOR alert[" + alert + "]";
				throw new Exception(msg + msig);	// I hate nullable types alltogether; can't return null here koz CHART=null is nonsence for the Dictionary
			}
			return this.Reverse[alert];
		}
		public List<ALERTS> FindContentsOf(CHART chart) {
			string msig = " //DictionaryManyToOne::FindContents(chart[" + chart + "])";
			if (this.Lookup.ContainsKey(chart) == false) {
				string msg = "NEVER_REGISTERED_IN_this.Lookup chart[" + chart + "]";
				throw new Exception(msg + msig);
			}
			return this.Lookup[chart];
		}
	}
}
