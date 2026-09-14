((System.Func<string>)(() => {
  var sb = new System.Text.StringBuilder();
  var nav = ES2Access.ModEntry.Navigator;
  var st = (ES2Access.Core.UI.Graph.GraphState)typeof(ES2Access.UI.GraphNavigator).GetField("_state", System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(nav);
  string target = "@@TARGET@@";
  int added = 0;
  for (int pass = 0; pass < 8; pass++) {
    var rr = nav.InspectRender();
    int before = added;
    for (int i = 0; i < rr.Order.Count; i++) {
      var n = rr.Order[i];
      if (!n.Expandable || n.Id == null) continue;
      string k = n.Id.StructuralKey == null ? "" : n.Id.StructuralKey.ToString();
      if (k.Length == 0) continue;
      if (!(target.StartsWith(k) || k.Contains(target))) continue;
      if (st.Expanded.Add(n.Id)) added++;
    }
    if (added == before) break;
  }
  var r = nav.InspectRender();
  // `added` is deliberately NOT printed: it counts expansions this call made, and the
  // second read of a pair finds the subtree already expanded, which would self-diff.
  sb.Append("target=").Append(target).Append('\n');
  int shown = 0;
  for (int i = 0; i < r.Order.Count; i++) {
    var n = r.Order[i];
    string k = n.Id == null || n.Id.StructuralKey == null ? "" : n.Id.StructuralKey.ToString();
    if (!k.Contains(target)) continue;
    shown++;
    sb.Append(ES2Access.Core.UI.Graph.GraphAnnouncer.ComposeFull(n)).Append("  [").Append(k).Append("]\n");
    var lines = ES2Access.UI.GraphNavigator.BufferLines(n);
    for (int j = 0; j < lines.Count; j++) sb.Append("    buf: ").Append(lines[j]).Append('\n');
  }
  sb.Append("nodes=").Append(shown).Append('\n');
  return sb.ToString();
}))()
