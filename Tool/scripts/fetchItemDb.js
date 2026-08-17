import fs from 'fs'
import path from 'path'
import { fileURLToPath } from 'url'

const __filename = fileURLToPath(import.meta.url)
const __dirname = path.dirname(__filename)

const NAMES_URL = 'https://db.sp-tushonka.com/api/item/names'
const ITEM_URL = id => `https://db.sp-tushonka.com/api/item/?id=${id}`

const OUT_FILE = path.join(__dirname, '..', 'public', 'itemDb.json')

async function fetchJson(url) {
  const res = await fetch(url)
  if (!res.ok) throw new Error(`HTTP ${res.status}: ${url}`)
  return res.json()
}

async function fetchDetails(ids) {
  const out = []
  let done = 0
  const concurrency = 30

  async function processOne(id) {
    try {
      const detail = await fetchJson(ITEM_URL(id))
      const props = detail.item?._props || {}
      const parentId = detail.item?._parent
      const isQuestItem = props.QuestItem === true
      const slots = Array.isArray(props.Slots) ? props.Slots : []
      const plateIds = slots.flatMap(s => {
        const filters = Array.isArray(s._props?.filters) ? s._props.filters : []
        return filters.map(f => f.Plate).filter(p => p && typeof p === 'string')
      })
      return {
        id,
        name: detail.locale?.Name || detail.locale?.ShortName || detail.item?._name || '',
        parentId: parentId || null,
        rarity: props.RarityPvE || null,
        skipped: isQuestItem,
        ammoDamage: typeof props.Damage === 'number' ? props.Damage : null,
        ammoArmorDamage: typeof props.ArmorDamage === 'number' ? props.ArmorDamage : null,
        ammoPen: typeof props.PenetrationPower === 'number' ? props.PenetrationPower : null,
        armorClass: typeof props.armorClass === 'string' || typeof props.armorClass === 'number' ? Number(props.armorClass) : null,
        plateIds,
      }
    } catch (err) {
      console.error(`Failed ${id}: ${err.message}`)
      return null
    } finally {
      done++
      if (done % 100 === 0) console.log(`Fetched ${done}/${ids.length} details`)
    }
  }

  for (let i = 0; i < ids.length; i += concurrency) {
    const chunk = ids.slice(i, i + concurrency)
    const results = await Promise.all(chunk.map(processOne))
    out.push(...results.filter(Boolean))
  }

  return out
}

async function run() {
  console.log('Fetching item names...')
  const namesData = await fetchJson(NAMES_URL)
  const entries = namesData.map(e => ({
    id: e.item._id,
    name: e.locale.Name || e.locale.ShortName || '',
    shortName: e.locale.ShortName || '',
    handbookParentId: e.handbook?.ParentId || null,
    price: typeof e.handbook?.Price === 'number' ? e.handbook.Price : null,
  }))
  console.log(`Found ${entries.length} item names.`)

  const details = await fetchDetails(entries.map(e => e.id))
  const detailById = new Map()
  const out = []

  for (let i = 0; i < entries.length; i++) {
    const entry = entries[i]
    const detail = details[i]
    if (!detail || detail.skipped) continue
    const item = {
      id: entry.id,
      name: entry.name,
      parentId: detail.parentId,
      rarity: detail.rarity,
      price: entry.price,
    }
    if (detail.ammoDamage !== null) item.ammoDamage = detail.ammoDamage
    if (detail.ammoArmorDamage !== null) item.ammoArmorDamage = detail.ammoArmorDamage
    if (detail.ammoPen !== null) item.ammoPen = detail.ammoPen
    if (detail.armorClass !== null) item.armorClass = detail.armorClass
    if (detail.plateIds.length > 0) item.plateIds = detail.plateIds
    out.push(item)
    detailById.set(entry.id, item)
  }

  // For plate carriers with armorClass 0, compute overall class from default plates
  for (const item of out) {
    if (item.plateIds && item.plateIds.length > 0 && (item.armorClass === 0 || item.armorClass === null)) {
      let sum = 0
      let count = 0
      for (const plateId of item.plateIds) {
        const plate = detailById.get(plateId)
        if (plate && typeof plate.armorClass === 'number' && plate.armorClass > 0) {
          sum += plate.armorClass
          count++
        }
      }
      if (count > 0) {
        item.armorClass = sum / count
      }
      delete item.plateIds
    }
  }

  // Fetch missing parent nodes so category ancestry is complete
  let missingIds = [...new Set(out.map(e => e.parentId).filter(Boolean))].filter(id => !detailById.has(id))
  let round = 1
  while (missingIds.length > 0) {
    console.log(`Fetching ${missingIds.length} missing parent node(s) (round ${round})...`)
    const nodes = await fetchDetails(missingIds)
    let added = 0
    for (const node of nodes) {
      if (!node || node.skipped) continue
      const entry = { id: node.id, name: node.name || node.id, parentId: node.parentId, rarity: null, price: null }
      if (node.armorClass !== null) entry.armorClass = node.armorClass
      out.push(entry)
      detailById.set(node.id, entry)
      added++
    }
    console.log(`Added ${added} nodes.`)
    missingIds = [...new Set(out.map(e => e.parentId).filter(Boolean))].filter(id => !detailById.has(id))
    round++
    if (round > 10) break
  }

  fs.mkdirSync(path.dirname(OUT_FILE), { recursive: true })
  fs.writeFileSync(OUT_FILE, JSON.stringify(out, null, 2))
  console.log(`Wrote ${out.length} items to ${OUT_FILE}`)
}

run().catch(err => {
  console.error(err)
  process.exit(1)
})
