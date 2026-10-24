using GimUtilities;
using NUnit.Framework;

namespace TestPlatform
{
	// Regression guards for the IFC BREP progress buckets. These mirror the production
	// protocol in GIMocctReadElements.LoadIfcDocWithProgress / ReadXcafDocumentToGimData:
	// all three buckets registered up front with fixed share weights, Load and Elements with
	// placeholder totals corrected mid-flight via AddTask. Under fixed weights the reported
	// percentage is monotone by construction - a total correction rescales only that bucket's
	// fraction (0/anything = 0 until its phase starts), never the visible boundary. These
	// tests lock that in: no freeze, no regression, and default-weight flows keep the plain
	// ratio engine semantics.
	public class GimProgressStatusTests
	{
		private const string BOOT = "IfcLoadBootstrap";
		private const string LOAD = "IfcLoadDoc";
		private const string ELEMENTS = "ElementsToRead";

		private const int W_BOOT = 150;
		private const int W_LOAD = 450;
		private const int W_ELEMENTS = 500;

		// The feature's real protocol: crawl during the silent stretches, exact-delta load
		// ticks, then the shapeCount correction (a 50x total balloon) - the bar must never
		// move backward or plateau, at any transition.
		[Test]
		public void WeightedIfcSequenceIsMonotoneWithNoSnapAtCorrection()
		{
			GimProgressStatus status = new();
			status.Create(
				new() { { BOOT, 4800 }, { LOAD, 1 }, { ELEMENTS, 1 } },
				new() { { BOOT, W_BOOT }, { LOAD, W_LOAD }, { ELEMENTS, W_ELEMENTS } });

			// parse window: synthetic crawl is bounded by its weight (150/1100 = 13.6%)
			status.EntityCompletedCallback(BOOT, 4800);
			Assert.That(status.PercentageDone, Is.InRange(13.5f, 13.7f));

			// handoff: Load gets its real total; completed is 0 either way, so the bar must not move
			double beforeHandoff = status.PercentageDone;
			status.AddTask(LOAD, 10000);
			Assert.That(status.PercentageDone, Is.EqualTo(beforeHandoff));

			// load phase: exact deltas, strictly climbing to the (150+450)/1100 = 54.5% share
			double previous = status.PercentageDone;
			for (int i = 0; i < 100; i++)
			{
				status.EntityCompletedCallback(LOAD, 100);
				Assert.That(status.PercentageDone, Is.GreaterThan(previous));
				previous = status.PercentageDone;
			}
			Assert.That(status.PercentageDone, Is.InRange(54.4f, 54.6f));

			// shapeCount correction: total balloons 10000 -> 500000 while completed stays 0 -
			// the visible boundary must not move (the old ratio engine snapped 50% -> 2% here,
			// which froze or regressed the bar depending on the clamp policy)
			double beforeCorrection = status.PercentageDone;
			status.AddTask(ELEMENTS, 500000);
			Assert.That(status.PercentageDone, Is.EqualTo(beforeCorrection));

			// elements phase: strictly climbing to 100, no plateau
			for (int i = 0; i < 50; i++)
			{
				status.EntityCompletedCallback(ELEMENTS, 10000);
				Assert.That(status.PercentageDone, Is.GreaterThan(previous));
				previous = status.PercentageDone;
			}
			status.ForceCompleteTask(ELEMENTS);
			Assert.That(status.PercentageDone, Is.EqualTo(100f));
		}

		// Backward compatibility: default weights (weight = total) preserve the plain ratio
		// engine for STEP/IGES/mesh/write flows (single Create, tick, ForceCompleteTask).
		[Test]
		public void DefaultWeightSingleBucketIsMonotoneAndEndsAtHundred()
		{
			GimProgressStatus status = new();
			status.Create(new() { { ELEMENTS, 100 } });

			Assert.That(status.PercentageDone, Is.EqualTo(0f));
			double previous = 0;
			for (int i = 0; i < 100; i++)
			{
				status.EntityCompletedCallback(ELEMENTS);
				Assert.That(status.PercentageDone, Is.GreaterThan(previous));
				previous = status.PercentageDone;
			}

			Assert.That(status.PercentageDone, Is.EqualTo(100f));
		}

		// Backward compatibility: multi-bucket default weights keep sum(completed)/sum(total).
		[Test]
		public void DefaultWeightMultiBucketMatchesRatioSemantics()
		{
			GimProgressStatus status = new();
			status.Create(new() { { "A", 100 }, { "B", 300 } });

			status.EntityCompletedCallback("A", 50); // 50 / (100+300) = 12.5%
			Assert.That(status.PercentageDone, Is.EqualTo(12.5f));

			status.EntityCompletedCallback("B", 100); // (50+100) / 400 = 37.5%
			Assert.That(status.PercentageDone, Is.EqualTo(37.5f));
		}

		// AddTask on a default-weight bucket keeps ratio semantics: the new bucket joins the
		// ratio at its own total.
		[Test]
		public void AddTaskWithDefaultWeightJoinsRatio()
		{
			GimProgressStatus status = new();
			status.Create(new() { { "A", 100 } });
			status.EntityCompletedCallback("A", 50);

			status.AddTask("B", 100); // 50 / 200 = 25%
			Assert.That(status.PercentageDone, Is.EqualTo(25f));
		}

		// AddTask's optional weight closes the symmetry gap with Create(totals, weights): a
		// bucket introduced purely via AddTask (never mentioned at Create time) can still get a
		// fixed share instead of being forced into weight = total.
		[Test]
		public void AddTaskWithExplicitWeightUsesFixedShareInsteadOfTotal()
		{
			GimProgressStatus status = new();
			status.Create(new() { { "A", 100 } }); // A: no explicit weight -> weight = total = 100

			// B is brand new here - never seen by Create. Give it a fixed weight of 300,
			// deliberately different from its total (100), which the old 2-arg AddTask could
			// never express (it always fell back to weight = total for unseen buckets).
			status.AddTask("B", 100, weight: 300);

			// totalWeight = 100 (A) + 300 (B) = 400.
			status.EntityCompletedCallback("A", 100); // A complete: weighted = 100*(100/100) = 100 -> 100/400 = 25%
			Assert.That(status.PercentageDone, Is.EqualTo(25f));

			status.EntityCompletedCallback("B", 100); // B complete too: weighted = 100 + 300 = 400 -> 400/400 = 100%
			Assert.That(status.PercentageDone, Is.EqualTo(100f));
		}

		// An explicit weight always overrides a weight already registered for that entity
		// (e.g. seeded by Create's weights map ahead of an AddTask total correction), per the
		// "explicit call always wins" contract on AddTask.
		[Test]
		public void AddTaskExplicitWeightOverridesPreRegisteredWeight()
		{
			GimProgressStatus status = new();
			status.Create(
				new() { { "A", 1 }, { "B", 100 } },
				new() { { "A", 450 }, { "B", 500 } });

			// Override A's pre-registered weight (450 -> 150) in the same call that corrects its
			// placeholder total (1 -> 10000), before A has reported any progress.
			status.AddTask("A", 10000, weight: 150);

			// totalWeight is now 150 (A, overridden) + 500 (B) = 650, not 950.
			status.EntityCompletedCallback("B", 100); // B complete: weighted = 500*(100/100) = 500 -> 500/650 ~= 76.9%
			Assert.That(status.PercentageDone, Is.InRange(76.8f, 77.0f));
		}

		// The actual production pattern (see GIMocctReadElements.LoadIfcDocWithProgress): a
		// bucket is seeded with an explicit weight at Create time, then AddTask is called one or
		// more times with no weight argument to correct its total. Each such call must preserve
		// the originally registered weight, not just the first one.
		[Test]
		public void RepeatedAddTaskWithoutWeightPreservesOriginallyRegisteredWeight()
		{
			GimProgressStatus status = new();
			status.Create(
				new() { { "LOAD", 1 }, { "ELEMENTS", 1 } },
				new() { { "LOAD", W_LOAD }, { "ELEMENTS", W_ELEMENTS } });

			status.AddTask("LOAD", 1000); // first correction, no weight given - keeps W_LOAD (450)
			status.AddTask("LOAD", 2000); // second correction, no weight given - still keeps W_LOAD (450)

			// totalWeight = 450 (LOAD) + 500 (ELEMENTS) = 950 throughout, unaffected by either total change.
			status.EntityCompletedCallback("LOAD", 2000); // LOAD complete: weighted = 450*(2000/2000) = 450 -> 450/950 ~= 47.37%
			Assert.That(status.PercentageDone, Is.InRange(47.3f, 47.5f));
		}

		// Overriding the weight of a bucket that has already reported progress is explicitly
		// documented as a footgun (it resets that bucket's own completed count, same as any
		// AddTask call, and can shift other buckets' contribution via the denominator) - lock in
		// that this is exactly what happens, rather than something quietly different.
		[Test]
		public void AddTaskWeightOverrideAfterProgressResetsThatBucketsCompletedCount()
		{
			GimProgressStatus status = new();
			status.Create(new() { { "A", 100 }, { "B", 100 } }); // default weights: A=100, B=100

			status.EntityCompletedCallback("A", 100); // A fully done: (100+0) / 200 = 50%
			Assert.That(status.PercentageDone, Is.EqualTo(50f));

			// Re-registering A via AddTask with an explicit weight resets its completed count to 0,
			// exactly like any other AddTask call - the bar is not expected to hold A's prior progress.
			status.AddTask("A", 100, weight: 300);

			// totalWeight = 300 (A) + 100 (B) = 400; A completed is back to 0, B still 0 -> 0%.
			// maxPercentageReported is a monotone latch on what PercentageDone *reports*, so this
			// raw drop still surfaces as the previously reported 50% until new progress exceeds it.
			Assert.That(status.PercentageDone, Is.EqualTo(50f));

			status.EntityCompletedCallback("B", 100); // B complete: weighted = 100*(100/100) = 100 -> 100/400 = 25%, below the latch
			Assert.That(status.PercentageDone, Is.EqualTo(50f)); // latch still holds - raw 25% is not reported

			status.EntityCompletedCallback("A", 100); // A complete again: weighted = 300+100 = 400 -> 400/400 = 100%, exceeds the latch
			Assert.That(status.PercentageDone, Is.EqualTo(100f));
		}
	}
}
