﻿using System;

using Sq1.Core.StrategyBase;
using Sq1.Core.Optimization;

namespace Sq1.Core.Optimization {
	public partial class Optimizer {
		public event	EventHandler<EventArgs>								OnOneBacktestStarted;
		// since Optimizer.backtests is multithreaded list, I imply OptimizerControl.backtests to keep its own copy for ObjectListView to freely crawl over it without interference (instead of providing Optimizer.BacktestsThreadSafeCopy)  
		public event	EventHandler<SystemPerformanceRestoreAbleEventArgs>	OnOneBacktestFinished;
		public event	EventHandler<EventArgs>								OnAllBacktestsFinished;
		public event	EventHandler<EventArgs>								OnOptimizationAborted;
		public 			EventHandler<EventArgs>								OnScriptRecompiledUpdateHeaderPostponeColumnsRebuild;
		

		public void RaiseOnBacktestStarted() {
			if (this.AbortedDontScheduleNewBacktests) return;
			if (this.OnOneBacktestStarted == null) {
				string msg = "OPTIMIZER_HAS_NO_SUBSCRIBERS_TO_NOTIFY_ABOUT_BACKTEST_STARTED";
				//SETTING_COLLAPSED_FROM_BTN_RUN_CLICK  Assembler.PopupException(msg);
				return;
			}
			try {
				this.OnOneBacktestStarted(this, EventArgs.Empty);
			} catch (Exception ex) {
				string msg = "OPTIMIZER_CONTROL_THREW_ON_BACKTEST_STARTED";
				Assembler.PopupException(msg, ex);
			}
		}
		public void RaiseOnOneBacktestFinished(SystemPerformance sysPerf) {
			if (this.AbortedDontScheduleNewBacktests) return;
			if (this.OnOneBacktestFinished == null) {
				string msg = "OPTIMIZER_HAS_NO_SUBSCRIBERS_TO_NOTIFY_ABOUT_BACKTEST_COMPLETED";
				Assembler.PopupException(msg);
				return;
			}
			try {
				this.BacktestsSecondsElapsed = (float) Math.Round(stopWatch.ElapsedMilliseconds / 1000d, 1);
				SystemPerformanceRestoreAble performanceEssence = new SystemPerformanceRestoreAble(sysPerf);
				this.OnOneBacktestFinished(this, new SystemPerformanceRestoreAbleEventArgs(performanceEssence));
			} catch (Exception ex) {
				string msg = "OPTIMIZER_CONTROL_THREW_ON_BACKTEST_COMPLETE";
				Assembler.PopupException(msg, ex);
			}
		}
		public void RaiseOnAllBacktestsFinished() {
			if (this.OnAllBacktestsFinished == null) {
				string msg = "OPTIMIZER_HAS_NO_SUBSCRIBERS_TO_NOTIFY_ABOUT_OPTIMIZATION_COMPLETE";
				Assembler.PopupException(msg);
				return;
			}
			try {
				stopWatch.Stop();
				this.BacktestsSecondsElapsed = (float) Math.Round(stopWatch.ElapsedMilliseconds / 1000d, 1);
				this.OnAllBacktestsFinished(this, EventArgs.Empty);
			} catch (Exception ex) {
				string msg = "OPTIMIZER_CONTROL_THREW_ON_OPTIMIZATION_COMPLETE";
				Assembler.PopupException(msg, ex);
			}
		}
		public void RaiseOnOptimizationAborted() {
			if (this.OnOptimizationAborted == null) {
				string msg = "OPTIMIZER_HAS_NO_SUBSCRIBERS_TO_NOTIFY_ABOUT_OPTIMIZATION_ABORTED";
				Assembler.PopupException(msg);
				return;
			}
			try {
				this.OnOptimizationAborted(this, EventArgs.Empty);
			} catch (Exception ex) {
				string msg = "OPTIMIZER_CONTROL_THREW_ON_OPTIMIZATION_ABORTED";
				Assembler.PopupException(msg, ex);
			}
		}

		//public bool ScriptRecompiledColumnsRebuildPostponed;
		public void RaiseScriptRecompiledUpdateHeaderPostponeColumnsRebuild() {
			//this.ScriptRecompiledColumnsRebuildPostponed = true;

			int scriptParametersTotalNr = this.ScriptParametersTotalNr;
			int indicatorParameterTotalNr = this.IndicatorParameterTotalNr;
			if (scriptParametersTotalNr == 0) {
				this.BacktestsTotal = indicatorParameterTotalNr;
			}
			if (indicatorParameterTotalNr == 0) {
				this.BacktestsTotal = scriptParametersTotalNr;
			}
			if (scriptParametersTotalNr > 0 && indicatorParameterTotalNr > 0) {
				this.BacktestsTotal = scriptParametersTotalNr * indicatorParameterTotalNr;
			}

			if (this.OnScriptRecompiledUpdateHeaderPostponeColumnsRebuild == null) return;
			this.OnScriptRecompiledUpdateHeaderPostponeColumnsRebuild(this, null);
		}
	}
}
