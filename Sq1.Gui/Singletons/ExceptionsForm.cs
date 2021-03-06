using System;
using System.Diagnostics;
using System.Threading.Tasks;

using Sq1.Core;

namespace Sq1.Gui.Singletons {
	public partial class ExceptionsForm : DockContentSingleton<ExceptionsForm> {
		public ExceptionsForm() : base() {
			this.InitializeComponent();
			
			// below I want to avoid {Assembler.instance=null}-related exception in DesignMode
			if (base.DesignMode) {
				#if DEBUG
				Debugger.Break();
				#endif
				throw new Exception("I doubt that a Form.ctor() could ever have base.DesignMode=true" +
					"; base() has not been informed yet that the form IS in DesignMode, right?...");
				//return; 
			}
			this.ExceptionControl.Initialize();
		}
		public void PopupException(string msg, Exception ex = null, bool debuggingBreak = true) {
			#if DEBUG
			if (debuggingBreak) {
				Debugger.Break();
			}
			#endif

			if (msg != null) ex = new Exception(msg, ex);

			//v1 SWITCHING_TO_GUI_THREAD_AS_ONE_STEP___MAY_GET_VERY_CLUMSY_WHEN_MANY_THREADS_POPUP_THEIR_EXCEPTIONS_AT_THE_SAME_TIME
			//base.ShowPopupSwitchToGuiThreadRunDelegateInIt(new Action(delegate {
			//    this.ExceptionControl.InsertSyncAndFlushListToTreeIfDockContentDeserialized_inGuiThread(ex);
			//}));
			//v2 TRYING_TO_1)LET_INVOKER_GO_EARLIER_FOR_FURTHER_2)QUEUEING_OF_LISTVIEW_REPAINT__2)NYI
			#region EXPERIMENTAL
			Task t = new Task(delegate {
				base.ShowPopupSwitchToGuiThreadRunDelegateInIt(new Action(delegate {
					this.ExceptionControl.InsertSyncAndFlushListToTreeIfDockContentDeserialized_inGuiThread(ex);
				}));
			});
			t.ContinueWith(delegate {
				string msg2 = "TASK_THREW_ExceptionsForm.popupException()";
				#if DEBUG
					Debugger.Break();
				#else
					Assembler.PopupException(msg2, t.Exception);
				#endif
			}, TaskContinuationOptions.OnlyOnFaulted);
			t.Start();
			#endregion
		}
		protected override void OnLoad(EventArgs e) {
			foreach (Exception beforeFormInstantiated in Assembler.InstanceInitialized.ExceptionsWhileInstantiating) {
				this.PopupException(null, beforeFormInstantiated, false);
			}
		}

		internal void UpdateConnectionStatus(Core.DataTypes.ConnectionState status, int statusCode, string message) {
			throw new NotImplementedException();
		}
	}
}