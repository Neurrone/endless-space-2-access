((System.Func<string>)(() => {
  var sb = new System.Text.StringBuilder();
  AgeTransform t = null;
  try { @@CONTAINER@@ } catch (System.Exception e) { return "container threw: " + e.GetType().Name; }
  if (t == null) return "container absent";
  System.Collections.IList ch = (System.Collections.IList)t.Children;
  if (ch == null) return "container has no child list";
  var rows = new System.Collections.ArrayList();
  for (int i = 0; i < ch.Count; i++) {
    AgeTransform c = (AgeTransform)ch[i];
    if (c == null) { rows.Add("(null child)"); continue; }
    var line = new System.Text.StringBuilder();
    var ls = c.GetComponentsInChildren<AgePrimitiveLabel>(true);
    int k = 0;
    // Only labels the card itself is DRAWING, tested up the chain to - but not including -
    // the pooled child: a label inside a hidden group stays Visible on its own transform and
    // keeps whatever the last subject that did draw it wrote, for good, so including those
    // makes every rebind read stale. The child's own Visible/Alpha is excluded from the test
    // because it is printed on the same line: a retired child must stay in the record, with
    // the subject it is still holding.
    for (int j = 0; j < ls.Length && k < 4; j++) {
      bool drawn = ls[j].AgeTransform != null;
      for (AgeTransform p = ls[j].AgeTransform; drawn && p != null; p = p.Parent) { if (p == c) break; if (!p.Visible || p.Alpha <= 0f) drawn = false; }
      if (!drawn) continue;
      string s = ls[j].Text; if (s != null && s.Length > 0 && s.Length < 60) { if (k > 0) line.Append(" / "); line.Append(s); k++; }
    }
    if (k == 0) line.Append("(no drawn text)");
    // One decimal: a child caught part-way through a fade lands on a slightly different
    // alpha on every run, and the third decimal of a fade is not what anyone is reading.
    line.Append("  |  ").Append(c.name).Append(" Visible=").Append(c.Visible).Append(" Alpha=").Append(c.Alpha.ToString("0.0"));
    rows.Add(line.ToString());
  }
  // Sorted by the bound subject, not left in slot order: the game reshuffles which pooled
  // child holds which subject between two binds, and that reshuffle is not the surface.
  // A retired child is still in the record - it sorts in under its old subject at Alpha 0.
  rows.Sort();
  sb.Append("children=").Append(ch.Count).Append('\n');
  for (int i = 0; i < rows.Count; i++) sb.Append((string)rows[i]).Append('\n');
  return sb.ToString();
}))()
