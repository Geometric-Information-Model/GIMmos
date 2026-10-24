using System;
using System.Collections.Generic;
using System.Linq;

namespace GimUtilities
{
	/// <summary>
	/// To use the progress bar you need to pas an instance of the GimProgressStatus class to 
	/// the ReadToGimDataAsync function or the WriteFromGimDataAsync function.+
	/// The asynchronous functions return a Task. This Task can be used to set an awaiter 
	/// (Something to do when we have the result), check if the function has failed or to get the result directly.
	/// </summary>
	public class GimProgressStatus
	{

		public const string CREATE_GLB = "CreateGLBs";
		public const string WRITE_TO_DISK = "WriteToDisk";
		public const string CREATE_GIMBIM = "CreateGIMbin";
        public const string READ_GIMBIN = "ReadGIMbin";

		private readonly object lockObject = new();

		private Dictionary<string, int> entityAndTotalCount;
		private readonly Dictionary<string, int> entityAndCompletedCount = new();
		private readonly Dictionary<string, int> entityWeights = new();
		private readonly Dictionary<string, TimeSpan> TimePerEntity = new();
		private readonly Dictionary<string, DateTime> lastFinishedTime = new();

		// High-water mark for PercentageDone. Buckets can be added mid-flight (AddTask) once
		// their real size becomes known, which can transiently shrink sum(completed)/sum(total)
		// even though real work only ever moves forward. Never report a regression.
		private float maxPercentageReported = 0f;

		/// <summary>
		/// Start time of this GimProgressStatus. 
		/// This is set to the moment of the creation of the GimProgressStatus.
		/// GimProgressStatus should be creation as short as possible before starting the function it is used with.
		/// </summary>
		public readonly DateTime startTime;

		/// <summary>
		/// Returns True if the GimProgressStatus has completed as many entities as it was told to complete.
		/// Otherwise false.
		/// </summary>
		public bool ThinksItsCompleted
		{
			get
			{
				if (entityAndTotalCount == null)
					return false;

				lock (lockObject)
				{
					foreach (string entity in entityAndTotalCount.Keys.ToArray())
					{
						if (entityAndTotalCount[entity] > entityAndCompletedCount[entity])
							return false;
					}
				}
				return true;
			}
		}

		/// <summary>
		/// Returns true if the GimProgressStatus is started.
		/// The Create() Function should be called to set the entityAndTotalCount, which
		/// tells the GimProgressStatus it has started.
		/// </summary>
		public bool IsStarted
		{
			get
			{
				return entityAndTotalCount != null;
			}
		}

		/// <summary>
		/// Percentage of the Entities that still need to be completed.
		/// </summary>
		public float PercentageLeft
		{
			get
			{
				return 100.0f - PercentageDone;
			}
		}

		/// <summary>
		/// Percentage of the entities that is completed.
		/// </summary>
		public float PercentageDone
		{
			get
			{

				if (entityAndTotalCount == null) return 0;

				lock (lockObject)
				{
					float raw = ComputeRawPercentage();
					if (raw > maxPercentageReported)
						maxPercentageReported = raw;
					return maxPercentageReported;
				}
			}
		}

		/// <summary>
		/// sum(weight * completed/total) / sum(weight) as a percentage, unclamped. Buckets
		/// default to weight = total (backward compatible with the plain ratio engine); callers
		/// that need fixed share weights (totals corrected mid-flight without moving the visible
		/// boundary) pass an explicit weight map to <see cref="Create(Dictionary{string,int}, Dictionary{string,int})"/>.
		/// Callers that mutate the bucket totals must hold <see cref="lockObject"/>.
		/// </summary>
		private float ComputeRawPercentage()
		{
			int totalWeight = 0;
			float weighted = 0;
			foreach (KeyValuePair<string, int> keyValuePair in entityAndTotalCount)
			{
				var key = keyValuePair.Key;
				int total = keyValuePair.Value;
				if (total <= 0) continue; // placeholder/zero total: contributes nothing until it has a real size
				int weight = entityWeights.TryGetValue(key, out int w) ? w : total;
				totalWeight += weight;
				weighted += weight * ((float)entityAndCompletedCount[key] / total);
			}
			return totalWeight > 0 ? Math.Clamp(weighted / totalWeight * 100.0f, 0f, 100.0f) : 0f;
		}

		/// <summary>
		/// A weak guess to the amount of time that the task still needs.
		/// </summary>
		public TimeSpan GuessedTimeLeft
		{
			get
			{

				if (entityAndTotalCount == null) return TimeSpan.Zero;

				if (ThinksItsCompleted)
					return new TimeSpan(0);

				if (!IsStarted)
					return TimeSpan.MaxValue;

				if (!TimePerEntity.All((t) => t.Value < new TimeSpan(1, 0, 1)) && TimePerEntity.Count > 0)
					return TimeSpan.MaxValue;

				TimeSpan guessedTimeLeft = new TimeSpan(0, 0, 0, 0, 100);

				lock (lockObject)
				{
					if (entityAndTotalCount != null)
					{
						foreach (string entity in entityAndTotalCount.Keys.ToArray())
						{
							guessedTimeLeft += (entityAndTotalCount[entity] - entityAndCompletedCount[entity]) * TimePerEntity[entity];
						}
					}
				}
				return guessedTimeLeft;
			}
		}

		/// <summary>
		/// The estimated time of completion.
		/// GuessedEndTime = startTime + GuessedTimeLeft
		/// </summary>
		public DateTime GuessedEndTime
		{
			get
			{
				return startTime + GuessedTimeLeft;
			}
		}

		/// <summary>
		/// Constructor for the GimProgressStatus.
		/// This sets the start time to the current system time.
		/// </summary>
		public GimProgressStatus()
		{
			startTime = DateTime.Now;
		}

		/// <summary>
		/// Sets the internal dictionary for all entities and their total count.
		/// This function can be called once per GimProgressStatus. Otherwise it will throw an Exception.
		/// </summary>
		/// <param name="entityAndTotalCount">All entities that will receive callbacks and their total count. </param>
		public void Create(Dictionary<string, int> entityAndTotalCount)
		{
			Create(entityAndTotalCount, null);
		}

		/// <summary>
		/// Same as <see cref="Create(Dictionary{string,int})"/>, but with explicit fixed share
		/// weights for each bucket. The reported percentage is sum(weight * completed/total) /
		/// sum(weight). Calling <see cref="AddTask(string, int, int?)"/> on the same entity with no
		/// explicit weight leaves the weight set here untouched, so that call only corrects the
		/// bucket's total and rescales its own fraction - the visible boundary never moves, and the
		/// bar stays monotone by construction. Passing an explicit weight to that method instead
		/// intentionally overrides the weight set here; see its docs for the monotonicity caveat
		/// that comes with doing so.
		/// </summary>
		/// <param name="entityAndTotalCount">All entities that will receive callbacks and their total count. </param>
		/// <param name="weights">Fixed share weights per entity. Missing entities default to weight = total.</param>
		public void Create(Dictionary<string, int> entityAndTotalCount, Dictionary<string, int> weights)
		{
			if (this.entityAndTotalCount != null)
				// TODO: Make it so we can use one progress status for multiple functions calls!
				throw new Exception("Can not use " + nameof(GimProgressStatus) + " more than one time. ");

			lock (lockObject)
			{
				this.entityAndTotalCount = entityAndTotalCount;

				// Seed weights: explicit map where given, otherwise the total (plain ratio engine).
				entityWeights.Clear();
				foreach (string entity in entityAndTotalCount.Keys.ToArray())
					entityWeights[entity] = weights != null && weights.TryGetValue(entity, out int w) ? w : entityAndTotalCount[entity];

				// Create entityAndCompletedDictionary.
				foreach (string entity in entityAndTotalCount.Keys.ToArray())
					entityAndCompletedCount.Add(entity, 0);

				// Create lastFinishedTime.
				foreach (string entity in entityAndTotalCount.Keys.ToArray())
					lastFinishedTime.Add(entity, startTime);

				// Create entityAndTime.
				foreach (string entity in entityAndTotalCount.Keys.ToArray())
					TimePerEntity.Add(entity, TimeSpan.MaxValue);
			}
		}
		/// <summary>
		/// Appends a new bucket to an already-started plan (see <see cref="Create"/>), or
		/// corrects the total of a bucket that was already registered (e.g. via <see cref="Create(Dictionary{string,int}, Dictionary{string,int})"/>
		/// with a placeholder total). Unlike Create, this can be called multiple times, letting
		/// buckets be added mid-flight once their total becomes known. PercentageDone iterates
		/// entityAndTotalCount live under the same lock, so this is safe to call
		/// while the bar is being read from another thread.
		/// </summary>
		/// <param name="entity">The name of the new bucket.</param>
		/// <param name="total">The total count for the new bucket.</param>
		/// <param name="weight">
		/// Fixed share weight for this bucket, mirroring <see cref="Create(Dictionary{string,int}, Dictionary{string,int})"/>'s
		/// <c>weights</c> parameter. When provided, it always overrides any weight already registered
		/// for this entity (explicit caller intent wins). When omitted (the default), a bucket that
		/// was already given a weight - typically via <c>Create</c>'s <c>weights</c> map, ahead of an
		/// <c>AddTask</c> total correction - keeps that weight unchanged; a bucket seen here for the
		/// first time falls back to weight = total (plain ratio engine behavior), same as before this
		/// parameter existed.
		/// <para>
		/// Caution: every call to this method resets the bucket's completed count to 0 (same as
		/// before this parameter existed), and passing an explicit weight additionally changes
		/// sum(weight) in <see cref="ComputeRawPercentage"/>'s denominator - which can shift the
		/// reported percentage contribution of <b>other</b> buckets even though those buckets'
		/// own completed counts are untouched. This is safe for a bucket seen here for the first
		/// time (it has contributed nothing yet), but overriding the weight of an already-active
		/// bucket resets its progress and can move the visible boundary - prefer fixing weights
		/// once, either at <see cref="Create(Dictionary{string,int}, Dictionary{string,int})"/>
		/// time or on a bucket's first <c>AddTask</c> call.
		/// </para>
		/// <para>
		/// Like <c>Create</c>'s <c>weights</c> map, <paramref name="weight"/> is not validated: a
		/// value &lt;= 0 makes the bucket contribute nothing to <c>PercentageDone</c> (weight 0) or
		/// produces a negative partial sum that gets clamped away (negative weight) - neither
		/// throws, but both are almost certainly caller mistakes.
		/// </para>
		/// </param>
		public void AddTask(string entity, int total, int? weight = null)
		{
			if (entityAndTotalCount == null)
				throw new Exception("Call Create() before AddTask().");
			lock (lockObject)
			{
				entityAndTotalCount[entity] = total;
				entityAndCompletedCount[entity] = 0;
				if (weight.HasValue)
					entityWeights[entity] = weight.Value; // explicit call always wins
				else if (!entityWeights.ContainsKey(entity))
					entityWeights[entity] = total; // bucket added mid-flight with no weight given: plain ratio engine behavior
				lastFinishedTime[entity] = DateTime.Now;
				TimePerEntity[entity] = TimeSpan.MaxValue;
			}
		}

		/// <summary>
		/// Reset the progress bar to be reused. Avoid if possible.
		/// </summary>
		/// <returns>False if the progressbar wasn't set (before the reset). True otherwise.</returns>
		public bool Reset()
		{
			bool isReset = entityAndTotalCount != null;
			lock (lockObject)
			{
				entityAndTotalCount = null;
				entityAndCompletedCount.Clear();
				entityWeights.Clear();
				TimePerEntity.Clear();
				lastFinishedTime.Clear();
				maxPercentageReported = 0f;
			}
			return isReset;
        }
		public bool WaitForStart() 
		{
			while (IsStarted == false) 
			{
				System.Threading.Thread.Sleep(25);
			}
			return true;
		}

		/// <summary>
		/// Callback function for when an entity is completed.
		/// This will increase the amount of completed for the entity by one.
		/// </summary>
		/// <param name="entity">The name of the entity that is completed.</param>
		public void EntityCompletedCallback(string entity)
		{
			lock (lockObject)
			{
				
				entityAndCompletedCount[entity]++;
				DateTime currentTime = DateTime.Now;
				TimePerEntity[entity] = currentTime - lastFinishedTime[entity];
				lastFinishedTime[entity] = currentTime;
                GimLogger.Info(entity + ": " + entityAndCompletedCount[entity]);
			}
		}

        /// <summary>
        /// Callback function for when an entity is completed.
        /// This will increase the amount of completed for the entity by count.
        /// </summary>
        /// <param name="entity">The name of the entity that is completed.</param>
        public void EntityCompletedCallback(string entity, int count)
		{
            lock (lockObject)
            {
                entityAndCompletedCount[entity] += count;
                DateTime currentTime = DateTime.Now;
                TimePerEntity[entity] = currentTime - lastFinishedTime[entity];
                lastFinishedTime[entity] = currentTime;
                GimLogger.Info(entity + ": " + entityAndCompletedCount[entity]);
            }
        }

		public void ForceCompleteTask(string task)
		{
			lock (lockObject)
			{
				if (entityAndCompletedCount[task] == entityAndTotalCount[task]) return;
				entityAndCompletedCount[task] = entityAndTotalCount[task];
				DateTime currentTime = DateTime.Now;
				TimePerEntity[task] = currentTime - lastFinishedTime[task];
				lastFinishedTime[task] = currentTime;
			}
		}
	}
}
