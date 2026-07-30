import { readdirSync, readFileSync, statSync, existsSync, writeFileSync } from 'fs'
import { join, dirname } from 'path'
import { fileURLToPath } from 'url'

const __dirname = dirname(fileURLToPath(import.meta.url))
const sptDir = process.env.SPT_DIR || 'C:\\SPT\\SPT'
const locationsDir = join(sptDir, 'SPT_Data', 'database', 'locations')
const outPath = join(__dirname, '..', 'src', 'extracts.ts')

if (!existsSync(locationsDir)) {
  console.warn(`[build-extracts] SPT locations dir not found: ${locationsDir}`)
  process.exit(0)
}

const raw = {}
for (const name of readdirSync(locationsDir)) {
  const sub = join(locationsDir, name)
  if (!statSync(sub).isDirectory()) continue
  const file = join(sub, 'allExtracts.json')
  if (!existsSync(file)) continue
  const data = JSON.parse(readFileSync(file, 'utf8'))
  if (!Array.isArray(data)) continue
  const names = [...new Set(data.map(e => e.Name).filter(Boolean))]
  if (names.length > 0) {
    raw[name] = names
  }
}

// Map SPT location folder names to the values used by the tool's MAP_LOCATIONS.
const KEY_MAP = {
  bigmap: 'bigmap',
  factory4_day: 'factory4_day',
  factory4_night: 'factory4_night',
  interchange: 'Interchange',
  laboratory: 'laboratory',
  lighthouse: 'Lighthouse',
  rezervbase: 'RezervBase',
  sandbox: 'Sandbox',
  shoreline: 'Shoreline',
  tarkovstreets: 'TarkovStreets',
  woods: 'Woods',
}

const result = {}
for (const [folder, key] of Object.entries(KEY_MAP)) {
  if (raw[folder]) {
    result[key] = raw[folder]
  }
}

// Composite 'factory4' covers both day and night variants.
const day = raw.factory4_day || []
const night = raw.factory4_night || []
result.factory4 = [...new Set([...day, ...night])]

// If a high-tier Ground Zero variant exists, keep it under Sandbox too.
if (raw.sandbox_high) {
  result.Sandbox = [...new Set([...(result.Sandbox || []), ...raw.sandbox_high])]
}

const content = `// Auto-generated from SPT allExtracts.json. Do not edit manually.
export const EXTRACTS_BY_LOCATION: Record<string, string[]> = ${JSON.stringify(result, null, 2)}
`

writeFileSync(outPath, content)
console.log(`[build-extracts] Wrote ${Object.keys(result).length} maps → ${outPath}`)
