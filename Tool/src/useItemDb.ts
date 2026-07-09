import { useState, useEffect } from 'react'

export interface ItemDbEntry {
  id: string
  name: string
  shortName?: string
  parentId: string | null
  rarity: string | null
  price: number | null
}

let itemDb: Map<string, ItemDbEntry> | null = null
let itemDbLoading = false
let itemDbListeners: Array<(db: Map<string, ItemDbEntry>) => void> = []

async function loadItemDb() {
  if (itemDb || itemDbLoading) return
  itemDbLoading = true
  try {
    const res = await fetch('/itemDb.json')
    const data: ItemDbEntry[] = await res.json()
    itemDb = new Map()
    for (const entry of data) {
      if (!entry?.id) continue
      itemDb.set(entry.id, entry)
    }
  } catch {
    itemDb = new Map()
  }
  itemDbLoading = false
  itemDbListeners.forEach(fn => fn(itemDb!))
  itemDbListeners = []
}

export function useItemDb() {
  const [db, setDb] = useState<Map<string, ItemDbEntry>>(new Map())

  useEffect(() => {
    if (itemDb) {
      setDb(itemDb)
    } else {
      itemDbListeners.push(setDb)
      loadItemDb()
    }
  }, [])

  return db
}

export function getItemDb(): Map<string, ItemDbEntry> | null {
  return itemDb
}

export function getItemDbEntry(id: string): ItemDbEntry | undefined {
  return itemDb?.get(id)
}

export function isItemInCategory(entry: ItemDbEntry, categoryId: string, db: Map<string, ItemDbEntry>): boolean {
  let current = entry.parentId
  while (current) {
    if (current === categoryId) return true
    const parent = db.get(current)
    if (!parent) return false
    current = parent.parentId
  }
  return false
}
