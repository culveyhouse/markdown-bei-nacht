# Markdown bei Nacht QA Playground

A friendly little document for checking headings, lists, tables, code, links, images, and safe media embeds.

## Text Styles

This paragraph has **bold text**, *italic text*, ***bold italic text***, `inline code`, and a [web link](https://example.com).

> A blockquote should feel calm, readable, and visually distinct from the surrounding text.
> It should also wrap nicely when the window is resized.

---

## Lists

- First unordered item
- Second unordered item with nested detail
  - Nested item A
  - Nested item B
- Third unordered item

1. First ordered item
2. Second ordered item
3. Third ordered item

- [x] Checked task item
- [ ] Unchecked task item

## Table

| Feature | Expected Result | Notes |
| --- | --- | --- |
| Headings | Clear hierarchy | H1, H2, H3 |
| Code blocks | Dark readable syntax | Highlight.js should run |
| Images | Render inline | Remote images are now allowed |
| Media | Native controls show | Audio/video tags are allowed |

## Code

```csharp
public static string SayGoodnight(string name)
{
    return $"Goodnight, {name}.";
}
```

```powershell
Get-ChildItem -Path . -Filter *.md
```

## Links

- [Jump to media](#media-embeds)
- [Open the project README](README.md)
- [Open a normal website](https://www.example.com)

## Remote Image

![A remote placeholder image with text saying Markdown bei Nacht](https://placehold.co/900x360/0b1320/73c7ff/png?text=Markdown+bei+Nacht+Remote+Image)

## Media Embeds

The viewer should show browser-native controls for these safe media embeds.

### Audio

<audio controls preload="metadata">
  <source src="https://www.w3schools.com/html/horse.mp3" type="audio/mpeg">
</audio>

### Video

<video controls width="720" poster="https://placehold.co/720x405/11233b/f4f8ff/png?text=Video+Poster">
  <source src="https://www.w3schools.com/html/mov_bbb.mp4" type="video/mp4">
</video>

## Unsupported Embed Check

This iframe should not render as an iframe:

<iframe src="https://example.com"></iframe>

## Plain Long Paragraph

This is a longer paragraph meant to test line height, wrapping, contrast, and comfortable reading width. Markdown bei Nacht should keep this readable without feeling cramped, even when the window is not maximized. If the preview is doing its job, this should feel like a quiet late-night reading surface rather than a busy editor panel.
