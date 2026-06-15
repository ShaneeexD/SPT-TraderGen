# Example Trader Pack — Viktor the Collector

This is an example trader pack for the TraderGen framework.

## Setup

1. Edit `trader.json` to customize the trader
2. Enable the trader in the json, it's the first line `"enabled": true,`
2. The TraderGen mod will automatically load this trader on server startup

## Notes

- The `id` must be a unique 24-character hex string
- Item template IDs (`itemTpl`) can be found in the SPT item database or wiki
- Comments in JSON are supported (the loader strips them)
- If the avatar file is missing, the trader will load but have no image
