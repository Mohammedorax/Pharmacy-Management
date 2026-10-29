using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace project_fo_3
{
    // Successful cart transitions only. Preference I/O never runs on the caller's UI thread.
    internal sealed class FrequentMedicineCartRecorder
    {
        private const int MaximumPending = 128;
        private readonly object gate = new object();
        private readonly Queue<string> pending = new Queue<string>();
        private readonly Func<bool> current;
        private readonly Func<string, Task> record;
        private readonly Action<Exception> report;
        private Task completion = Task.CompletedTask;
        private bool draining;

        internal FrequentMedicineCartRecorder(FrequentMedicineDesktopSession session, Action<Exception> report = null)
            : this(() => session.IsCurrent, name => session.RecordCartAdditionAsync(name), report)
        { if (session == null) throw new ArgumentNullException(nameof(session)); }

        internal FrequentMedicineCartRecorder(Func<bool> current, Func<string, Task> record, Action<Exception> report = null)
        {
            this.current = current ?? throw new ArgumentNullException(nameof(current));
            this.record = record ?? throw new ArgumentNullException(nameof(record));
            this.report = report;
        }

        internal bool TryRecordSuccessfulAddition(string canonicalName)
        {
            // Preserve canonical names exactly, including mixed Arabic/Latin text.
            if (string.IsNullOrWhiteSpace(canonicalName) || canonicalName.Length > 200) return false;
            lock (gate)
            {
                if (pending.Count >= MaximumPending || !IsCurrent()) return false;
                pending.Enqueue(canonicalName);
                if (!draining)
                {
                    draining = true;
                    completion = Task.Run(DrainAsync);
                }
                return true;
            }
        }

        internal Task Completion { get { lock (gate) return completion; } }

        private bool IsCurrent()
        {
            try { return current(); }
            catch (Exception error) { Report(error); return false; }
        }

        private async Task DrainAsync()
        {
            while (true)
            {
                string name;
                lock (gate)
                {
                    if (!IsCurrent()) pending.Clear();
                    if (pending.Count == 0) { draining = false; return; }
                    name = pending.Dequeue();
                }
                try { await record(name).ConfigureAwait(false); }
                catch (OperationCanceledException) { }
                catch (Exception error) { Report(error); }
            }
        }

        private void Report(Exception error)
        {
            // A failing diagnostics sink cannot turn a successful sale-cart action into failure.
            try { report?.Invoke(error); } catch { }
        }
    }
}
