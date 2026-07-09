import { useState, useEffect } from 'react'
import { useItemDb } from './useItemDb'

export function useItemNames(itemIds: string[]) {
  const [names, setNames] = useState<Map<string, string>>(new Map())
  const db = useItemDb()

  useEffect(() => {
    if (itemIds.length === 0) return
    const result = new Map<string, string>()
    itemIds.forEach(id => {
      const entry = db.get(id)
      if (entry?.name) result.set(id, entry.name)
    })
    setNames(result)
  }, [itemIds.join(','), db])

  return names
}
