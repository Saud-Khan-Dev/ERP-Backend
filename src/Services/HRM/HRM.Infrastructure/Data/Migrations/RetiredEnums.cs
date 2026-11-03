/// Types of features since removed, kept only because the migrations that created (and the one that drops) their
/// tables name them. Nothing else may use them.
/// ReviewStatus: the performance (PER) reviews, removed 2026-10-28.
public enum ReviewStatus { Draft, Submitted, Acknowledged, Finalized, Countersigned }
