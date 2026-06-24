import { useState, useRef, useEffect } from 'react'
import { Plus, Trash2, ChevronDown } from 'lucide-react'
import type { AssortChildItem } from './types'

const SLOTS_BY_CATEGORY: Record<string, string[]> = {
  'Weapon': ['mod_pistol_grip', 'mod_stock', 'mod_magazine', 'mod_muzzle', 'mod_reciever', 'mod_barrel', 'mod_sight_rear', 'mod_sight_front', 'mod_gas_block', 'mod_handguard', 'mod_foregrip', 'mod_scope', 'mod_tactical', 'mod_bipod', 'mod_launcher', 'mod_nvg', 'mod_mount', 'mod_charge', 'mod_tactical_000', 'mod_flashlight', 'mod_mount_001', 'mod_stock_000'],
  'Armour': ['Front_plate', 'Back_plate', 'Left_plate', 'Right_plate', 'Soft_armor_front', 'Soft_armor_back', 'Soft_armor_left', 'soft_armor_right', 'Groin', 'Groin_back', 'Collar', 'Shoulder_l', 'Shoulder_r'],
  'Helmet': ['Helmet_top', 'Helmet_back', 'Helmet_ears', 'Helmet_visor'],
  'Gear': ['Pockets', 'SecuredContainer', 'Vest', 'Backpack', 'FaceCover', 'Eyewear', 'Earpiece', 'Headwear'],
  'Magazine': ['cartridges'],
  'Other': ['hideout', 'Foldable', 'Togglable'],
}

export function SlotPicker({ onSelect }: { onSelect: (slotId: string) => void }) {
  const [open, setOpen] = useState(false)
  const ref = useRef<HTMLDivElement>(null)

  useEffect(() => {
    function handleClick(e: MouseEvent) {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false)
    }
    if (open) document.addEventListener('mousedown', handleClick)
    return () => document.removeEventListener('mousedown', handleClick)
  }, [open])

  return (
    <div ref={ref} className="relative">
      <button onClick={() => setOpen(!open)}
        className="px-2 py-1.5 text-xs btn-secondary flex items-center gap-1 h-[38px] whitespace-nowrap"
        title="Pick a slot name">
        <ChevronDown size={12} />
      </button>
      {open && (
        <div className="absolute right-0 top-full mt-1 w-72 max-h-96 overflow-y-auto bg-tarkov-surface border border-tarkov-border rounded-lg shadow-xl z-50 p-2">
          <div className="text-xs text-tarkov-text-dim px-2 py-1">Click a slot to fill it in</div>
          {Object.entries(SLOTS_BY_CATEGORY).map(([cat, slots]) => (
            <div key={cat} className="mb-2">
              <div className="text-xs font-semibold text-tarkov-accent px-2 py-1">{cat}</div>
              <div className="grid grid-cols-2 gap-1">
                {slots.map(s => (
                  <button key={s} onClick={() => { onSelect(s); setOpen(false) }}
                    className="text-xs text-left px-2 py-1 rounded hover:bg-tarkov-accent/20 text-tarkov-text transition-colors truncate">
                    {s}
                  </button>
                ))}
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  )
}

export function ChildItemTree({
  children,
  path,
  onAdd,
  onRemove,
  onUpdate,
}: {
  children: AssortChildItem[]
  path: number[]
  onAdd: (path: number[]) => void
  onRemove: (path: number[]) => void
  onUpdate: (path: number[], key: keyof AssortChildItem, value: unknown) => void
}) {
  if (children.length === 0 && path.length === 0) {
    return <p className="text-xs text-tarkov-text-dim">No child items. Add plates, helmet attachments, weapon parts, etc.</p>
  }

  return (
    <div className="space-y-1">
      {children.map((child, ci) => {
        const childPath = [...path, ci]
        const depth = path.length
        return (
          <div key={ci}>
            <div className="flex items-end gap-2" style={{ marginLeft: `${depth * 16}px` }}>
              <div className="flex-1">
                {depth > 0 && <div className="h-px bg-tarkov-border/50 mb-1" />}
                <label className="label text-[11px]">Item Template ID {depth > 0 ? `(nested lvl ${depth})` : ''}</label>
                <input className="input-field font-mono text-sm" value={child.itemTpl}
                  onChange={e => onUpdate(childPath, 'itemTpl', e.target.value)}
                  placeholder="24-char hex ID" maxLength={24} />
              </div>
              <div className="w-44 relative">
                <label className="label text-[11px]">Slot ID</label>
                <div className="flex gap-1">
                  <input className="input-field text-sm flex-1" value={child.slotId}
                    onChange={e => onUpdate(childPath, 'slotId', e.target.value)}
                    placeholder="e.g. Front_plate" />
                  <SlotPicker onSelect={slotId => onUpdate(childPath, 'slotId', slotId)} />
                </div>
              </div>
              <div className="w-20">
                <label className="label text-[11px]">Amount</label>
                <input type="number" className="input-field text-sm text-center" min={1}
                  value={child.amount ?? 1}
                  onChange={e => {
                    const v = parseInt(e.target.value) || 1
                    onUpdate(childPath, 'amount', v === 1 ? undefined : v)
                  }} />
              </div>
              <button onClick={() => onRemove(childPath)}
                className="text-tarkov-error hover:text-tarkov-error/80 mb-2">
                <Trash2 size={14} />
              </button>
            </div>

            {/* Nested children */}
            {child.children && child.children.length > 0 && (
              <ChildItemTree
                children={child.children}
                path={childPath}
                onAdd={onAdd}
                onRemove={onRemove}
                onUpdate={onUpdate}
              />
            )}

            {/* Add sub-item button */}
            <div style={{ marginLeft: `${(depth + 1) * 16}px` }}>
              <button onClick={() => onAdd(childPath)}
                className="text-[11px] text-tarkov-accent hover:text-tarkov-accent-hover flex items-center gap-1 mt-1 mb-2">
                <Plus size={10} /> Add sub-item
              </button>
            </div>
          </div>
        )
      })}
      {children.length > 0 && (
        <div className="pt-1">
          <button onClick={() => onAdd(path)}
            className="text-[11px] text-tarkov-accent hover:text-tarkov-accent-hover flex items-center gap-1">
            <Plus size={10} /> Add Attachment
          </button>
        </div>
      )}
    </div>
  )
}
