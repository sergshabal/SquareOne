﻿using System.Collections.Generic;
using System.Threading;

using Sq1.Core.Execution;

namespace Sq1.Core.Broker {
	public class OrderPostProcessorSequencerCloseThenOpen {
		private object dictionaryLock;
		private Dictionary<List<Order>, List<Order>> sequencerLockCloseOpen;
		//private List<Order> ordersClose;
		private OrderProcessor orderProcessor;

		public OrderPostProcessorSequencerCloseThenOpen(OrderProcessor orderProcessor) {
			this.dictionaryLock = new object();
			this.sequencerLockCloseOpen = new Dictionary<List<Order>, List<Order>>();
			this.orderProcessor = orderProcessor;
		}
		public void InitializeSequence(List<Order> ordersClose, List<Order> ordersOpen) {
			lock (this.dictionaryLock) {
				this.sequencerLockCloseOpen.Add(ordersClose, ordersOpen);
				foreach (Order sequenced in ordersOpen) {
					OrderStateMessage omsg = new OrderStateMessage(sequenced, OrderState.SubmittingSequenced,
						"sequence initialized: [" + sequenced.State + "]=>[" + OrderState.SubmittingSequenced + "]"
						+ " for [" + ordersOpen.Count + "] fellow ordersOpen"
						+ " by [" + ordersClose.Count + "]ordersClose");
					this.orderProcessor.UpdateOrderStateAndPostProcess(sequenced, omsg);
				}
			}
		}
		public void ReplaceLockingCloseOrder(Order orderRejected, Order orderReplacement) {
			lock (this.dictionaryLock) {
				Dictionary<List<Order>, List<Order>> sequencerLockCloseOpenCopy = new Dictionary<List<Order>, List<Order>>(this.sequencerLockCloseOpen);
				foreach (List<Order> lockingCloseOrders in sequencerLockCloseOpenCopy.Keys) {
					List<Order> sequencedOpensAffected = this.sequencerLockCloseOpen[lockingCloseOrders];
					if (lockingCloseOrders.Contains(orderRejected) == false) continue;

					List<Order> lockingCloseWithReplaced = new List<Order>(lockingCloseOrders);
					lockingCloseWithReplaced.Add(orderReplacement);
					lockingCloseWithReplaced.Remove(orderRejected);
					this.sequencerLockCloseOpen.Add(lockingCloseWithReplaced, sequencedOpensAffected);
					this.sequencerLockCloseOpen.Remove(lockingCloseOrders);
				}
			}
		}
		public void OrderFilledUnlockSequenceSubmitOpening(Order orderClosed) {
			List<List<Order>> lockingClosesFound = new List<List<Order>>();
			lock (this.dictionaryLock) {
				// if among all the keys we have an order, then we should have Open orders sequenced
				foreach (List<Order> lockingCloseOrders in this.sequencerLockCloseOpen.Keys) {
					if (lockingCloseOrders.Contains(orderClosed) == false) continue;
					lockingClosesFound.Add(lockingCloseOrders);
				}
				// analyzing all locks for all symbols to find out whether this closed order released the lock
				foreach (List<Order> lockingCloseFound in lockingClosesFound) {
					// delete the locking order from the list (most likely containing 1 order)
					lockingCloseFound.Remove(orderClosed);
					if (lockingCloseFound.Count > 0) continue;

					// delete the list of locks from global dictionary
					List<Order> ordersOpen = this.sequencerLockCloseOpen[lockingCloseFound];
					this.sequencerLockCloseOpen.Remove(lockingCloseFound);
					this.orderProcessor.AppendOrderMessageAndPropagateCheckThrowOrderNull(orderClosed,
						"last CloseOpenSequence order filled, unlocking submission of [" 
						+ ordersOpen.Count + "]ordersOpen");
					if (ordersOpen.Count == 0) continue;

					// submitting all released opening orders
					foreach (Order submitting in ordersOpen) {
						OrderStateMessage omsg = new OrderStateMessage(submitting, OrderState.Submitting,
							"sequence cleared: [" + submitting.State + "]=>[" + OrderState.Submitting + "]"
							+ " for [" + ordersOpen.Count + "] fellow ordersOpen"
							+ " by orderClose=[" + orderClosed + "]");
						this.orderProcessor.UpdateOrderStateAndPostProcess(submitting, omsg);
					}
					BrokerAdapter broker = ordersOpen[0].Alert.DataSource.BrokerAdapter;
					ThreadPool.QueueUserWorkItem(new WaitCallback(broker.SubmitOrdersThreadEntryDelayed),
						new object[] { ordersOpen, ordersOpen[0].Alert.Bars.SymbolInfo.SequencedOpeningAfterClosedDelayMillis });
				}
			}
		}
	}
}
