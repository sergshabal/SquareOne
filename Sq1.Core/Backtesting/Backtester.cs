using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

using Sq1.Core.DataTypes;
using Sq1.Core.Execution;
using Sq1.Core.StrategyBase;
using Sq1.Core.Streaming;
using Sq1.Core.Livesim;

namespace Sq1.Core.Backtesting {
	public class Backtester {
		public const string				BARS_BACKTEST_CLONE_PREFIX		= "BACKTEST_BARS_CLONED_FROM_";
		public ScriptExecutor			Executor						{ get; private set; }

		public Bars						BarsOriginal					{ get; protected set; }
		public Bars						BarsSimulating					{ get; protected set; }
		public BacktestDataSource		BacktestDataSource				{ get; protected set; }
		   BacktestQuoteBarConsumer 	backtestQuoteBarConsumer;

		protected bool					BacktestWasAbortedByUserInGui;
		public ManualResetEvent			RequestingBacktestAbort			{ get; private set; }	// Calling ManualResetEvent.Set opens the gate, allowing any number of threads calling WaitOne to be let through
		public ManualResetEvent			BacktestAborted					{ get; private set; }
		public ManualResetEvent			BacktestIsRunning				{ get; private set; }
		

		public int						BarsSimulatedSoFar				{ get; protected set; }
		public int						QuotesTotalToGenerate			{ get {
				if (this.BarsOriginal == null) return -1;	// I_RESTORED_CONTEXT__END_OF_BACKTEST_ORIGINAL_BECAME_NULL
				return this.BarsOriginal.Count * this.QuotesGenerator.QuotePerBarGenerates;
			} }
		public int						QuotesGeneratedSoFar			{ get { return BarsSimulatedSoFar * this.QuotesGenerator.QuotePerBarGenerates; } }
		public BacktestMode				BacktestMode					{ get {
			if (this.QuotesGenerator == null) return BacktestMode.Unknown;
			else return this.QuotesGenerator.BacktestModeSuitsFor;
		} }
		public BacktestQuotesGenerator	QuotesGenerator					{ get; private set; }

		public string					ProgressStats					{ get {
				if (this.QuotesGenerator == null) return "QuotesGenerator=null";
				return this.QuotesGeneratedSoFar + " / " + this.QuotesTotalToGenerate;
			} }

		public bool						IsLivesimRunning				{ get { return (this as Livesimulator) != null; } }
		public bool						IsBacktestRunning				{ get { return this.BacktestIsRunning.WaitOne(0); } }
		public bool						IsBacktestingNoLivesimNow		{ get { return this.IsBacktestRunning == true && this.IsLivesimRunning == false; } }
		public bool						IsBacktestingLivesimNow			{ get { return this.IsBacktestRunning == true && this.IsLivesimRunning == true; } }

		public bool						WasBacktestAborted				{ get {
				if (this.QuotesGenerator == null) {
				    string msg = "ABORTION_IS_A_FLAG_IRRELEVANT_TO_QUOTE_GENERATOR_LIFECYCLE WORKED_FOR_BACKTEST_BUT_SPOILED_LATE_LIVESIM_CHECK";
				    return false;
				}
				bool signalled = this.BacktestAborted.WaitOne(0);
				return signalled;
			} }
		public int						ExceptionsHappenedSinceBacktestStarted;

		Backtester() {
			BacktestWasAbortedByUserInGui	= false;
			RequestingBacktestAbort			= new ManualResetEvent(false);
			BacktestAborted					= new ManualResetEvent(false);
			BacktestIsRunning				= new ManualResetEvent(false);
			backtestQuoteBarConsumer		= new BacktestQuoteBarConsumer(this);
			BacktestDataSource				= new BacktestDataSource();
			ExceptionsHappenedSinceBacktestStarted = 0;
		}
		public Backtester(ScriptExecutor executor) : this() {
			Executor = executor;
			if (Executor.Strategy == null) return;
			if (Executor.Strategy.Script == null) return;
			this.InitializeAndCheck(Executor.Strategy.ScriptContextCurrent.BacktestMode);
		}
		public void InitializeAndCheck(BacktestMode mode = BacktestMode.FourStrokeOHLC) {
			if (this.Executor.Bars == null) {
				string msg = "EXECUTOR_LOST_ITS_BARS_NONSENSE null==this.Executor.Bars SubstituteBarsAndRunSimulation()";
				//Assembler.PopupException(msg);
				throw new Exception(msg);
				return;
			}
			if (this.Executor.Bars.Count < 1) {
				string msg = "EXECUTOR_BARS_ARE_EMPTY_CAN_NOT_SUBSTITUTE_AND_REGENERATE";
				//Assembler.PopupException(msg);
				throw new Exception(msg);
				return;
			}

			if (this.BacktestMode == mode) return;
			switch (mode) {
				case BacktestMode.FourStrokeOHLC:
					this.QuotesGenerator = new BacktestQuotesGeneratorFourStroke(this);
					break;
				default:
					string msg = "NYI: [" + this.Executor.Strategy.ScriptContextCurrent.BacktestMode + "]RunSimulation";
					Assembler.PopupException(msg);
					throw new Exception(msg);
			}
		}
		public void SimulationRun() {
			this.ExceptionsHappenedSinceBacktestStarted = 0;
			//string msg = "MAKE_SURE_WE_WILL_INVOKE_BacktestStartingConstructOwnValuesValidateParameters()";
			//Assembler.PopupException(msg, null, false);

			int repaintableChunk = (int)(this.BarsOriginal.Count / 20);
			if (repaintableChunk <= 0) repaintableChunk = 1;
				
			int excludeLastBarStreamingWillTriggerIt = this.BarsOriginal.Count - 1;
			for (int barNo = 0; barNo <= excludeLastBarStreamingWillTriggerIt; barNo++) {
				Bar bar = this.BarsOriginal[barNo];

				bool abortRequested = this.RequestingBacktestAbort.WaitOne(0);
				if (abortRequested) {
					this.BacktestWasAbortedByUserInGui = true;
					break;
				}
				this.generateQuotesForBarAndPokeStreaming(bar);
				this.BarsSimulatedSoFar++;
				if (this.BarsSimulatedSoFar % repaintableChunk == 0) {
					this.Executor.EventGenerator.RaiseOnBacktesterSimulatedChunk_step3of4();
				}
					
				//MAKE_EXCEPTIONS_FORM_INSERT_DELAYED!!! Application.DoEvents();	// otherwize UI becomes irresponsible;
				//COMMENTED_OUT_TO_SIMULATE_PROFILER_BEHAVIOUR MORE_EXCEPTIONS_DISPLAYED_IN_EXCEPTIONS_FORM_WOW Application.DoEvents();
				//#if DEBUG
				// UNCOMMENTED_FOR_SHARP_DEVELOP_TO_NOT_FREAK_OUT_FULLY_EXPAND_LOCAL_VARIABLES_AT_BREAKPOINTS_RANDOMLY_CONTINUE_ETC IRRELATED_TO_EXCEPTIONS_THERE_WAS_NONE
				// COMMENTED_OUT_#DEVELOP_FREAKS_OUT_WHEN_YOU_MOVE_INNER_WINDOWS_SPLITTER Application.DoEvents();
				// UNCOMMENTED_TO_KEEP_MOUSE_OVER_SLIDERS_RESPONSIVE
				// NOT_NEEDED_WHEN_BACKTESTER_STARTS_WITHOUT_PARAMETERS Application.DoEvents();
				//#endif
			}

			string stats = " [" + this.BarsSimulatedSoFar + "/" + this.BarsOriginal.Count + "]";
			if (this.BacktestWasAbortedByUserInGui) {
				string msg = "USER_ABORTED_BACKTEST_IN_GUI_SOMEWHERE_BACKTESTED_ONLY_BARS" + stats;
			} else {
				string msg = "HERE_I_FINISHED_BACKTESTING_ALL_REQUIRED_BARS" + stats;
			}

			// see Indicator.DrawValue() "DONT_WANT_TO_HACK_WILL_DRAW_LAST_STATIC_BARS_INDICATOR_VALUE_AFTER_YOU_TURN_ON_STREAMING_SO_I_WILL_HAVE_NEW_QUOTE_PROVING_THE_LAST_BAR_IS_FORMED"
			// this.QuotesGenerator.InjectFakeQuoteInNonExistingNextBarToSolidifyLastStaticBar(this.Executor.Bars.BarStaticLast);

		}
		public void AbortRunningBacktestWaitAborted(string whyAborted, int millisecondsToWait = 1000) {
			if (this.IsBacktestRunning == false) return;

			//bool abortIsAlreadyRequested = this.RequestingBacktestAbort.WaitOne(0);
			this.RequestingBacktestAbort.Set();
			//bool abortAlreadyRequestedNow = this.RequestingBacktestAbort.WaitOne(0);

			string msig = " whyAborted=[" + whyAborted + "]: Strategy[" + this.Executor.Strategy + "] on Bars[" + this.Executor.Bars + "]";

			string msg = "BACKTEST_ABORTING";
			Assembler.PopupException(msg + msig, null, false);

			bool aborted = this.BacktestAborted.WaitOne(millisecondsToWait);
			msg = (aborted) ? "BACKTEST_ABORTED" : "BACKTESTER_DIDNT_ABORT_WITHIN_SECONDS[" + millisecondsToWait + "]";
			Assembler.PopupException(msg + msig, null, false);
		}

		public void AbortBacktestIfExceptionsLimitReached() {
			if (this.IsBacktestingNoLivesimNow == false) return;
			this.ExceptionsHappenedSinceBacktestStarted++;
			if (this.ExceptionsHappenedSinceBacktestStarted < this.Executor.Strategy.ExceptionsLimitToAbortBacktest) return;
			this.AbortRunningBacktestWaitAborted("AbortBacktestIfExceptionsLimitReached[" + this.Executor.Strategy.ExceptionsLimitToAbortBacktest + "]");
		}
		void closePositionsLeftOpenAfterBacktest() {
			//return;
			foreach (Alert alertPending in this.Executor.ExecutionDataSnapshot.AlertsPending.InnerListSafeCopy) {
				try {
					//if (alertPending.IsEntryAlert) {
					//	this.Executor.ClosePositionWithAlertClonedFromEntryBacktestEnded(alertPending);
					//} else {
					//	string msg = "checkPositionCanBeClosed() will later interrupt the flow saying {Sorry I don't serve alerts.IsExitAlert=true}";
					//	this.Executor.RemovePendingExitAlertPastDueClosePosition(alertPending);
					//}
					//bool removed = this.Executor.ExecutionDataSnapshot.AlertsPending.Remove(alertPending);
					this.Executor.AlertKillPending(alertPending);
				} catch (Exception e) {
					string msg = "NOT_AN_ERROR BACKTEST_POSITION_FINALIZER: check innerException: most likely you got POSITION_ALREADY_CLOSED on counterparty alert's force-close?";
					this.Executor.PopupException(msg, e);
				}
			}
			if (this.Executor.ExecutionDataSnapshot.AlertsPending.Count > 0) {
				string msg = "KILLING_LEFTOVER_ALERTS_DIDNT_WORK_OUT snap.AlertsPending.Count["
					+ this.Executor.ExecutionDataSnapshot.AlertsPending.Count + "] should be ZERO";
				Assembler.PopupException(msg, null, false);
			}

			foreach (Position positionOpen in this.Executor.ExecutionDataSnapshot.PositionsOpenNow.InnerListSafeCopy) {
				//v1 List<Alert> alertsSubmittedToKill = this.Executor.Strategy.Script.PositionCloseImmediately(positionOpen, );
				//v2 WONT_CLOSE_POSITION_EARLIER_THAN_OPENED Alert exitAlert = this.Executor.Strategy.Script.ExitAtMarket(this.Executor.Bars.BarStaticLastNullUnsafe, positionOpen, "BACKTEST_ENDED_EXIT_FORCED");
				Alert exitAlert = this.Executor.Strategy.Script.ExitAtMarket(this.Executor.Bars.BarStreaming, positionOpen, "BACKTEST_ENDED_EXIT_FORCED");
				if (exitAlert != positionOpen.ExitAlert) {
					string msg = "FIXME_SOMEHOW";
					Assembler.PopupException(msg);
				}
				// BETTER WOULD BE TO KILL PREVIOUS PENDING ALERT FROM A CALLBACK AFTER MARKET EXIT ORDER GETS FILLED, IT'S UNRELIABLE EXIT IF WE KILL IT HERE
				// LOOK AT EMERGENCY CLASSES, SOLUTION MIGHT BE THERE ALREADY
				//List<Alert> alertsSubmittedToKill = this.Executor.Strategy.Script.PositionKillExitAlert(positionOpen, "BACKTEST_ENDED_EXIT_FORCED");
				//v3 this.Executor.ExecutionDataSnapshot.MovePositionOpenToClosed(positionOpen);
				//v4
				if (positionOpen.ExitAlert == null) continue;
				try {
					this.Executor.RemovePendingExitAlertAndClosePositionAfterBacktestLeftItHanging(positionOpen.ExitAlert);
				} catch (Exception ex) {
					Assembler.PopupException("closePositionsLeftOpenAfterBacktest()", ex, false);
				}
			}
			if (this.Executor.ExecutionDataSnapshot.PositionsOpenNow.Count > 0) {
				string msg = "DIDNT_CLOSE_BACKTEST_LEFTOVER_POSITIONS snap.PositionsOpenNow.Count["
					+ this.Executor.ExecutionDataSnapshot.PositionsOpenNow.Count + "]";
				Assembler.PopupException(msg, null, false);
			}
		}
		protected virtual void SimulationPreBarsSubstitute_overrideable() {
			if (this.BarsOriginal == this.Executor.Bars) {
				string msg = "DID_YOU_FORGET_TO_RESET_this.BarsOriginal_TO_NULL_AFTER_BACKTEST_FINISHED??";
				Assembler.PopupException(msg);
			}
			try {
				//COPIED_UPSTACK_FOR_BLOCKING_MOUSEMOVE_AFTER_BACKTEST_NOW_CLICK__BUT_ALSO_STAYS_HERE_FOR_SLIDER_CHANGE_NON_INVALIDATION
				//WONT_BE_RESET_IF_EXCEPTION_OCCURS_BEFORE_TASK_LAUNCH
				if (this.Executor.ChartShadow != null) this.Executor.ChartShadow.PaintAllowedDuringLivesimOrAfterBacktestFinished = false;

				this.BarsOriginal = this.Executor.Bars;
				this.BarsSimulating = this.Executor.Bars.CloneNoBars(BARS_BACKTEST_CLONE_PREFIX + this.BarsOriginal);
				this.Executor.EventGenerator.RaiseOnBacktesterBarsIdenticalButEmptySubstitutedToGrow_step1of4();
				
				#region candidate for this.BacktestDataSourceBuildFromUserSelection()
				BacktestSpreadModeler spreadModeler;
				// kept it on the surface and didn't pass ScriptContextCurrent.SpreadModelerPercent to "new BacktestDataSource()" because later BacktestDataSource:
				// 1) will support different SpreadModelers with not only 1 parameter like SpreadModelerPercentage;
				// 2) will support different BacktestModes like 12strokes, not only 4Stroke 
				// 3) will poke StreamingAdapter-derived implementations 12 times a bar with platform-generated quotes for backtests with regulated poke delay
				// 4) will need to be provide visualized 
				// v1 this.BacktestDataSource.BacktestStreamingAdapter.InitializeSpreadModelerPercentage(this.Executor.Strategy.ScriptContextCurrent.SpreadModelerPercent);
				// v2 UI-controlled in the future, right now the stub  
				ContextScript ctx = this.Executor.Strategy.ScriptContextCurrent;
				string msig = "Strategy[" + this.Executor.Strategy + "].ScriptContextCurrent[" + ctx + "]";
				switch (ctx.SpreadModelerClassName) {
					case "BacktestSpreadModelerPercentage":
						spreadModeler = new BacktestSpreadModelerPercentage(this.Executor.Strategy.ScriptContextCurrent.SpreadModelerPercent);
						break;
					default:
						string msg = "SPREAD_MODELER_NOT_YET_SUPPORTED[" + ctx.SpreadModelerClassName + "]"
							+ ", instantiatind default BacktestSpreadModelerPercentage("
							+ this.Executor.Strategy.ScriptContextCurrent.SpreadModelerPercent + ")";
						Assembler.PopupException(msg + msig);
						spreadModeler = new BacktestSpreadModelerPercentage(this.Executor.Strategy.ScriptContextCurrent.SpreadModelerPercent);
						break;
				}
				this.BacktestDataSource.Initialize(this.BarsSimulating, spreadModeler);
				#endregion

				this.BarsSimulating.DataSource = this.BacktestDataSource;

				StreamingAdapter streaming = this.BacktestDataSource.StreamingAdapter;
				DataDistributor distr = streaming.DataDistributor;
				distr.ConsumerQuoteSubscribe(this.BarsSimulating.Symbol, this.BarsSimulating.ScaleInterval, this.backtestQuoteBarConsumer, false);
				distr.ConsumerBarSubscribe  (this.BarsSimulating.Symbol, this.BarsSimulating.ScaleInterval, this.backtestQuoteBarConsumer, false);
				streaming.SetQuotePumpThreadNameSinceNoMoreSubscribersWillFollowFor(this.BarsSimulating.Symbol, this.BarsSimulating.ScaleInterval);
				
				this.Executor.BacktestContextInitialize(this.BarsSimulating);

				if (this.BarsOriginal == null) {
					string msg = "consumers will expect this.BarsOriginal != null";
					Assembler.PopupException(msg);
				}
				if (this.BarsOriginal.Count == 0) {
					string msg = "consumers will expect this.BarsOriginal.Count > 0";
					Assembler.PopupException(msg);
				}
				if (this.BarsSimulating == null) {
					string msg = "consumers will expect this.BarsSimulating != null";
					Assembler.PopupException(msg);
				}
				if (this.BarsSimulating.Count > 0) {
					string msg = "consumers will expect this.BarsSimulating.Count = 0";
					Assembler.PopupException(msg);
				}
				if (this.Executor.Bars == null) {
					string msg = "consumers will expect this.Bars != null";
					Assembler.PopupException(msg);
				}
				if (this.Executor.Bars.Count > 0) {
					string msg = "consumers will expect this.Bars.Count = 0";
					Assembler.PopupException(msg);
				}

				//ALREADY_RAISED_INSIDE_CONTEXT_INITIALIZE() this.Executor.EventGenerator.RaiseOnBacktesterSimulationContextInitialized_step2of4();
			} catch (Exception ex) {
				string msg = "PreBarsSubstitute(): Backtester caught a long beard...";
				this.Executor.PopupException(msg, ex);
			} finally {
				this.BarsSimulatedSoFar = 0;
				this.BacktestWasAbortedByUserInGui = false;
				this.BacktestAborted.Reset();
				this.RequestingBacktestAbort.Reset();
				this.BacktestIsRunning.Set();
				if (this.IsBacktestRunning == false) {
					string msg = "IN_ORDER_TO_SIGNAL_UNFLAGGED_I_HAVE_TO_RESET_INSTEAD_OF_SET";
					Assembler.PopupException(msg);
				}
			}
		}
		protected virtual void SimulationPostBarsRestore_overrideable() {
			try {
				StreamingAdapter streamingBacktest = this.BacktestDataSource.StreamingAdapter;
				StreamingAdapter streamingOriginal = this.BarsOriginal.DataSource.StreamingAdapter;
				string msg = "NOW_INSERT_BREAKPOINT_TO_this.channel.PushQuoteToConsumers(quoteDequeued) CATCHING_BACKTEST_END_UNPAUSE_PUMP";

				if (streamingOriginal != null) {
					string msg2 = "BRO_THIS_IS_NONSENSE!!!";
					streamingOriginal.AbsorbStreamingBarFactoryFromBacktestComplete(streamingBacktest, this.BarsOriginal.Symbol, this.BarsOriginal.ScaleInterval);
				}

				DataDistributor distr = this.BacktestDataSource.StreamingAdapter.DataDistributor;
				distr.ConsumerQuoteUnsubscribe	(this.BarsSimulating.Symbol, this.BarsSimulating.ScaleInterval, this.backtestQuoteBarConsumer);
				distr.ConsumerBarUnsubscribe	(this.BarsSimulating.Symbol, this.BarsSimulating.ScaleInterval, this.backtestQuoteBarConsumer);

				this.Executor.BacktestContextRestore();
				this.BarsOriginal = null;	// I_RESTORED_CONTEXT__END_OF_BACKTEST_ORIGINAL_BECAME_NULL WILL_AFFECT_ChartForm.TsiProgressBarETA
				if (this.Executor.ChartShadow == null) {
					string msg3 = "IAM_IN_OPTIMIZER_HAVING_NO_CHART_ASSOCIATED";
					return;
				}
				this.Executor.ChartShadow.PaintAllowedDuringLivesimOrAfterBacktestFinished = true;
			} catch (Exception e) {
				#if DEBUG
				Debugger.Break();
				#endif
				throw e;
			} finally {
				// Calling ManualResetEvent.Set opens the gate,
				// allowing any number of threads calling WaitOne to be let through
				//moved to this.NotifyWaitingThreads()
				//this.BacktestCompletedQuotesCantGo.Set();
				if (this.BacktestWasAbortedByUserInGui) {
					this.BacktestAborted.Set();
					this.RequestingBacktestAbort.Reset();
				}
			}
		}
		void generateQuotesForBarAndPokeStreaming(Bar bar2simulate) {
			if (bar2simulate == null) return;
			if (bar2simulate.IsBarStreaming && double.IsNaN(bar2simulate.Open)) {
				string msg = "IRRELEVANT_PARTIAL_VALUES_WERE_DEPRECATED it's ok for Bars.LastBar from Repository to have no PartialValues;"
					+ " filled by Streaming, NA for Backtest, skipping LastBar";
				//throw new Exception(msg);
				Assembler.PopupException(msg);
				return;
			}

			List<QuoteGenerated> quotesGenerated = this.QuotesGenerator.GenerateQuotesFromBarAvoidClearing(bar2simulate);
			if (quotesGenerated == null) return;
			for (int i = 0; i < quotesGenerated.Count; i++) {
				if (this.IsBacktestRunning == false) break;

				QuoteGenerated quote = quotesGenerated[i];
				
				#if DEBUG //TEST_EMEDDED
				if (quote.ParentBarSimulated.ParentBarsIndex != bar2simulate.ParentBarsIndex) {
					Debugger.Break();
				}

				// PREV_QUOTE_ABSNO_SHOULD_NOT_BE_LINEAR_CAN_CONTAIN_HOLES_DUE_TO_QUOTES_INJECTED_TO_FILL_ALERTS
				//QuoteGenerated quotePrev;
				//if (i > 0) {
				//	quotePrev = quotesGenerated[i-1];
				//	if (quote.Absno != quotePrev.Absno + 1) {
				//		//string msg = "IRRELEVANT since GenerateQuotesFromBar() has been upgraded to return SortedList<int, QuoteGenerated> instead of randomized List<QuoteGenerated>";
				//		string msg = "PREV_QUOTE_ABSNO_MUST_BE_LINEAR_WITHOUT_HOLES STILL_RELEVANT FIXME";
				//		//Debugger.Break();
				//	}
				//}
				#endif

				#if DEBUG //TEST_EMBEDDED GENERATED_QUOTE_OUT_OF_BOUNDARY_CHECK #1/2
				if (bar2simulate.HighLowDistance > 0 && bar2simulate.HighLowDistance > quote.Spread
						&& i > 0 && bar2simulate.ContainsBidAskForQuoteGenerated(quote) == false) {
					Debugger.Break();
				}
				#endif

				int pendingsToFillInitially = this.Executor.ExecutionDataSnapshot.AlertsPending.Count;
				List<QuoteGenerated> quotesInjected = this.QuotesGenerator.InjectQuotesToFillPendingAlerts(quote, bar2simulate);
				if (quotesInjected.Count > 0 && quote.AbsnoPerSymbol != this.QuotesGenerator.LastGeneratedAbsnoPerSymbol) {
					//DONT_FORGET_TO_ASSIGN_LATEST_ABSNO_TO_QUOTE_TO_REACH
					#if DEBUG //TEST_EMBEDDED
					if (quotesInjected.Count != this.QuotesGenerator.LastGeneratedAbsnoPerSymbol - quote.AbsnoPerSymbol) {
						string msg = "InjectQuotesToFillPendingAlerts()_INCREMENTED_QUOTE_ABSNO";
						//Debugger.Break();
					}
					#endif
					if (quote.AbsnoPerSymbol != -1 && quote.AbsnoPerSymbol != this.QuotesGenerator.LastGeneratedAbsnoPerSymbol) {	//DONT_FORGET_TO_ASSIGN_LATEST_ABSNO_TO_QUOTE_TO_REACH
						string msg = "SO_WHY_ABSNO_MUST_BE_SET_HERE_AND_CANT_BE_SET_IN_QUOTE.CTOR?...";
						quote.AbsnoPerSymbol  = this.QuotesGenerator.LastGeneratedAbsnoPerSymbol;
					}
				}
				
				#if DEBUG //TEST_EMBEDDED
				if (quotesInjected.Count == 0) {
					string msg = "SEEMS_ONLY_STOP_ALERTS_FAR_BEYOND_TARGET_ARE_ON_THE_WAY; pendingsToFillInitially[" + pendingsToFillInitially + "]"
						+ "STOPS_ARE_TOO_FAR OR_WILL_BE_FILLED_NEXT_THING_UPSTACK";
				}
				if (quotesInjected.Count == pendingsToFillInitially) {
					int pendingsLeft = this.Executor.ExecutionDataSnapshot.AlertsPending.Count;
					string msg = "GENERATED_EXACTLY_AS_MANY_AS_PENDINGS; PENDINGS_UNFILLED_LEFT_" + pendingsLeft;
				}
				int pendingsStrategyJustGenerated = quotesInjected.Count - pendingsToFillInitially;
				if (pendingsStrategyJustGenerated > 0) {
					string msg = "SEEMS_STRATEGY_GENERATED_NEW_ALERTS_ON_NEW_QUOTES"
						+ "; quotesInjected.Count[" + quotesInjected.Count + "] > pendingsToFillInitially[" + pendingsToFillInitially + "]";
				}
				if (pendingsStrategyJustGenerated < 0) {
					string msg = "STOP_ALERTS_IGNORED_OTHERS_FILLED";
				}
				#endif

				int pendingsLeftAfterInjected = this.Executor.ExecutionDataSnapshot.AlertsPending.Count;

				this.BacktestDataSource.StreamingAsBacktestNullUnsafe.GeneratedQuoteEnrichSymmetricallyAndPush(quote, bar2simulate);
				quote.WentThroughStreamingToScript = true;

				//nothing poductive below, only breakpoint placeholders
				#if DEBUG //TEST_EMEDDED
				int pendingsLeftAfterTargetQuoteGenerated = this.Executor.ExecutionDataSnapshot.AlertsPending.Count;
				if (pendingsToFillInitially == 0) continue;

				int pendingsFilledByInjected = pendingsLeftAfterInjected - pendingsToFillInitially;
				if (pendingsFilledByInjected > 0) {
					string msg = "SEEMS_LIKE_INJECTING_DOES_ITS_JOB; pendingsFilledByInjected[" + pendingsFilledByInjected + "]";
				}
				int targetQuoteIsntExpectedToFillAnything = pendingsLeftAfterTargetQuoteGenerated - pendingsLeftAfterInjected;
				if (targetQuoteIsntExpectedToFillAnything > 0) {
					string msg = "SEEMS_LIKE_TARGET_QUOTE_HAD_OWN_FILL POSSIBLE_BUT_UNLIKELY";
				}
				#endif
			}
		}
		public const string TO_STRING_PREFIX = "BACKTESTER_FOR_";
		public override string ToString() {
			string ret = TO_STRING_PREFIX + this.Executor.ToString();
			return ret;
		}

		public void InitializeAndRun_step1of2() {
			this.InitializeAndCheck();
			this.SimulationPreBarsSubstitute_overrideable();
			this.SimulationRun();
		}
		public void BacktestRestore_step2of2() {
			if (this.IsBacktestingLivesimNow == false) {
				this.closePositionsLeftOpenAfterBacktest();
			}
			this.SimulationPostBarsRestore_overrideable();
			this.BacktestIsRunning.Reset();
			if (this.IsBacktestRunning) {
				string msg = "IN_ORDER_TO_SIGNAL_FLAGGED_I_HAVE_TO_SET_INSTEAD_OF_RESET";
				Assembler.PopupException(msg);
			}
		}
	}
}
