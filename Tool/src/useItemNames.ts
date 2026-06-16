import { useState, useEffect } from 'react'

let itemNameDb: Map<string, string> | null = null
let itemNameDbLoading = false
let itemNameDbListeners: Array<() => void> = []

async function loadItemNameDb() {
  if (itemNameDb || itemNameDbLoading) return
  itemNameDbLoading = true
  try {
    const res = await fetch('https://db.sp-tarkov.com/api/item/names')
    const data = await res.json()
    itemNameDb = new Map()
    for (const entry of data) {
      const id = entry?.item?._id
      const name = entry?.locale?.Name || entry?.locale?.ShortName
      if (id && name) itemNameDb.set(id, name)
    }
  } catch {
    itemNameDb = new Map()
  }
  itemNameDbLoading = false
  itemNameDbListeners.forEach(fn => fn())
  itemNameDbListeners = []
}

export function useItemNames(itemIds: string[]) {
  const [names, setNames] = useState<Map<string, string>>(new Map())

  useEffect(() => {
    if (itemIds.length === 0) return

    const resolve = () => {
      if (!itemNameDb) return
      const result = new Map<string, string>()
      itemIds.forEach(id => {
        const name = itemNameDb!.get(id)
        if (name) result.set(id, name)
      })
      setNames(result)
    }

    if (itemNameDb) {
      resolve()
    } else {
      itemNameDbListeners.push(resolve)
      loadItemNameDb()
    }
  }, [itemIds.join(',')])

  return names
}
