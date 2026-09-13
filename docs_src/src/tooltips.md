# Tooltips

Virtually every element has a tooltip. Tooltips can themselves trigger nested tooltips shown when hovering over specific words. This is exposed as children. Use `Right` to expand and `Up` and `Down` to navigate nested tooltips, `Left` to collapse.

Short text-only tooltips are always automatically read. In [mod settings](mod-settings.md), you can also disable the reading of longer tooltips.

Tooltips can be read line by line in the [UI Buffer](buffers.md)

The game has two options that I recommend changing:

- Tooltip delay: change this to 0
- Tooltip display: when set to progressive, the key info of the tooltip is shown first. After a configurable delay, non-essential flavour text is shown. The mod works with this setting by reading only the initial tooltip contents, leaving the flavour text in the buffer.
