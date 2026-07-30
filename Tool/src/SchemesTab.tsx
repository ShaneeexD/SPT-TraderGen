import { useMemo, useState } from 'react'
import { Plus, Trash2, ChevronDown, ChevronUp, HelpCircle } from 'lucide-react'
import SearchableSelect from './SearchableSelect'
import { useItemDb } from './useItemDb'
import type { ProductionSchemeDefinition, SchemeRequirement } from './types'
import { generateMongoId } from './types'

interface SchemesTabProps {
  schemes: ProductionSchemeDefinition[]
  onChange: (schemes: ProductionSchemeDefinition[]) => void
}

function createDefaultScheme(): ProductionSchemeDefinition {
  return {
    _id: generateMongoId(),
    areaType: 10,
    endProduct: '',
    count: 1,
    productionTime: 300,
    needFuelForAllProductionTime: false,
    unlockedByDefault: false,
    requirements: [],
  }
}

function createDefaultRequirement(): SchemeRequirement {
  return { type: 'Item' }
}

const HIDEOUT_AREAS: { value: number; label: string }[] = [
  { value: 0, label: 'Vents' },
  { value: 1, label: 'Security' },
  { value: 2, label: 'Water Closet' },
  { value: 3, label: 'Stash' },
  { value: 4, label: 'Generator' },
  { value: 5, label: 'Heating' },
  { value: 6, label: 'Water Collector' },
  { value: 7, label: 'Med Station' },
  { value: 8, label: 'Kitchen' },
  { value: 9, label: 'Rest Space' },
  { value: 10, label: 'Workbench' },
  { value: 11, label: 'Intelligence Center' },
  { value: 12, label: 'Shooting Range' },
  { value: 13, label: 'Library' },
  { value: 14, label: 'Scav Case' },
  { value: 15, label: 'Illumination' },
  { value: 16, label: 'Place of Fame' },
  { value: 17, label: 'Air Filtering Unit' },
  { value: 18, label: 'Solar Power' },
  { value: 19, label: 'Booze Generator' },
  { value: 20, label: 'Bitcoin Farm' },
  { value: 21, label: 'Christmas Illumination' },
  { value: 22, label: 'Emergency Wall' },
  { value: 23, label: 'Gym' },
  { value: 24, label: 'Weapon Stand' },
  { value: 25, label: 'Weapon Stand Secondary' },
  { value: 26, label: 'Equipment Presets Stand' },
  { value: 27, label: 'Circle of Cultists' },
]

function Field({ label, tooltip, error, children }: {
  label: string
  tooltip?: string
  error?: boolean
  children: React.ReactNode
}) {
  return (
    <div>
      <label className={`block text-xs mb-1 ${error ? 'text-tarkov-error' : 'text-tarkov-text-dim'} flex items-center gap-1.5`}>
        {label}
        {tooltip && (
          <span className="relative group">
            <HelpCircle size={13} className="text-tarkov-text-dim hover:text-tarkov-accent cursor-help transition-colors" />
            <span className="absolute bottom-full left-1/2 -translate-x-1/2 mb-2 px-3 py-2 bg-tarkov-bg border border-tarkov-border rounded-lg text-xs text-tarkov-text font-normal w-64 opacity-0 invisible group-hover:opacity-100 group-hover:visible transition-all duration-150 z-50 shadow-xl leading-relaxed pointer-events-none">
              {tooltip}
            </span>
          </span>
        )}
      </label>
      {children}
    </div>
  )
}

function Toggle({ label, tooltip, value, onChange }: {
  label: string
  tooltip?: string
  value: boolean
  onChange: (v: boolean) => void
}) {
  return (
    <label className="flex items-center gap-2 text-xs text-tarkov-text-dim cursor-pointer">
      <input
        type="checkbox"
        checked={value}
        onChange={e => onChange(e.target.checked)}
        className="rounded border-tarkov-border/50"
      />
      {label}
      {tooltip && (
        <span className="relative group">
          <HelpCircle size={13} className="text-tarkov-text-dim hover:text-tarkov-accent cursor-help transition-colors" />
          <span className="absolute bottom-full left-1/2 -translate-x-1/2 mb-2 px-3 py-2 bg-tarkov-bg border border-tarkov-border rounded-lg text-xs text-tarkov-text font-normal w-64 opacity-0 invisible group-hover:opacity-100 group-hover:visible transition-all duration-150 z-50 shadow-xl leading-relaxed pointer-events-none">
            {tooltip}
          </span>
        </span>
      )}
    </label>
  )
}

export default function SchemesTab({ schemes, onChange }: SchemesTabProps) {
  const itemDb = useItemDb()
  const [expanded, setExpanded] = useState<Set<number>>(new Set())

  const itemOptions = useMemo(() => {
    const opts: { value: string; label: string; sub: string }[] = []
    for (const entry of itemDb.values()) {
      if (!entry?.id) continue
      opts.push({
        value: entry.id,
        label: entry.name || entry.shortName || entry.id,
        sub: entry.shortName || entry.id,
      })
    }
    return opts
  }, [itemDb])

  const updateScheme = (index: number, patch: Partial<ProductionSchemeDefinition>) => {
    const next = [...schemes]
    next[index] = { ...next[index], ...patch }
    onChange(next)
  }

  const removeScheme = (index: number) => {
    const next = schemes.filter((_, i) => i !== index)
    onChange(next)
    setExpanded(prev => {
      const nextSet = new Set(prev)
      nextSet.delete(index)
      const adjusted = new Set<number>()
      for (const idx of nextSet) {
        if (idx > index) adjusted.add(idx - 1)
        else if (idx < index) adjusted.add(idx)
      }
      return adjusted
    })
  }

  const addScheme = () => {
    onChange([...schemes, createDefaultScheme()])
    setExpanded(prev => new Set([...Array.from(prev), schemes.length]))
  }

  const toggleExpand = (index: number) => {
    setExpanded(prev => {
      const next = new Set(prev)
      if (next.has(index)) next.delete(index)
      else next.add(index)
      return next
    })
  }

  const expandAll = () => setExpanded(new Set(schemes.map((_, i) => i)))
  const collapseAll = () => setExpanded(new Set())

  const updateRequirement = (schemeIndex: number, reqIndex: number, patch: Partial<SchemeRequirement>) => {
    const next = [...schemes]
    const scheme = { ...next[schemeIndex] }
    scheme.requirements = [...scheme.requirements]
    scheme.requirements[reqIndex] = { ...scheme.requirements[reqIndex], ...patch }
    next[schemeIndex] = scheme
    onChange(next)
  }

  const addRequirement = (schemeIndex: number) => {
    const next = [...schemes]
    const scheme = { ...next[schemeIndex] }
    scheme.requirements = [...scheme.requirements, createDefaultRequirement()]
    next[schemeIndex] = scheme
    onChange(next)
  }

  const removeRequirement = (schemeIndex: number, reqIndex: number) => {
    const next = [...schemes]
    const scheme = { ...next[schemeIndex] }
    scheme.requirements = scheme.requirements.filter((_, i) => i !== reqIndex)
    next[schemeIndex] = scheme
    onChange(next)
  }

  return (
    <div className="space-y-4 p-4">
      <div className="flex items-center justify-between flex-wrap gap-2">
        <h2 className="text-lg font-semibold text-tarkov-accent">Production Schemes</h2>
        <div className="flex items-center gap-2">
          <button
            onClick={expandAll}
            className="btn-secondary text-xs px-2 py-1"
            disabled={schemes.length === 0}
          >
            Expand All
          </button>
          <button
            onClick={collapseAll}
            className="btn-secondary text-xs px-2 py-1"
            disabled={schemes.length === 0}
          >
            Collapse All
          </button>
          <button
            onClick={addScheme}
            className="btn-secondary text-xs flex items-center gap-1 px-2 py-1"
          >
            <Plus size={12} /> Add Scheme
          </button>
        </div>
      </div>

      {schemes.length === 0 && (
        <p className="text-sm text-tarkov-text-dim">No custom production schemes defined.</p>
      )}

      <div className="space-y-2">
        {schemes.map((scheme, sIdx) => {
          const area = HIDEOUT_AREAS.find(a => a.value === scheme.areaType)
          const endProduct = itemDb.get(scheme.endProduct)
          const endProductLabel = endProduct?.name || scheme.endProduct || 'No end product'
          const isExpanded = expanded.has(sIdx)

          return (
            <div key={sIdx} className="bg-tarkov-bg rounded-lg border border-tarkov-border/50 overflow-visible">
              <button
                onClick={() => toggleExpand(sIdx)}
                className="w-full flex items-center justify-between p-3 hover:bg-tarkov-border/20 transition-colors text-left"
              >
                <div className="flex items-center gap-3 min-w-0">
                  {isExpanded ? <ChevronUp size={14} className="text-tarkov-text-dim flex-shrink-0" /> : <ChevronDown size={14} className="text-tarkov-text-dim flex-shrink-0" />}
                  <div className="min-w-0">
                    <div className="text-sm font-semibold">Scheme #{sIdx + 1}</div>
                    <div className="text-xs text-tarkov-text-dim truncate">
                      {area?.label ?? `Area ${scheme.areaType}`} → {endProductLabel}
                    </div>
                  </div>
                </div>
                <div className="flex items-center gap-1 flex-shrink-0">
                  <span className="text-xs text-tarkov-text-dim mr-2 hidden sm:inline">
                    {scheme.requirements.length} req{scheme.requirements.length !== 1 ? 's' : ''}
                  </span>
                  <div
                    onClick={(e) => {
                      e.stopPropagation()
                      removeScheme(sIdx)
                    }}
                    className="text-tarkov-error hover:text-tarkov-error/80 p-1 cursor-pointer"
                    role="button"
                    tabIndex={0}
                  >
                    <Trash2 size={14} />
                  </div>
                </div>
              </button>

              {isExpanded && (
                <div className="p-4 pt-0 space-y-3 border-t border-tarkov-border/30">
                  <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
                    <Field label="Scheme ID" tooltip="Unique 24-character hex ID for this recipe. Must match the recipe ID used in quest rewards if this scheme is locked behind a quest.">
                      <input
                        type="text"
                        className="input-field text-xs font-mono w-full"
                        value={scheme._id}
                        onChange={e => updateScheme(sIdx, { _id: e.target.value })}
                        maxLength={24}
                      />
                    </Field>
                    <Field label="Area Type" tooltip="The hideout area this craft appears in, e.g. Workbench (10) or Med Station (7).">
                      <select
                        className="input-field text-xs w-full"
                        value={scheme.areaType}
                        onChange={e => updateScheme(sIdx, { areaType: Number(e.target.value) })}
                      >
                        {HIDEOUT_AREAS.map(a => (
                          <option key={a.value} value={a.value}>{a.value} — {a.label}</option>
                        ))}
                      </select>
                    </Field>
                    <Field label="Production Time (s)" tooltip="How long the craft takes in seconds.">
                      <input
                        type="number"
                        className="input-field text-xs w-full"
                        value={scheme.productionTime}
                        onChange={e => updateScheme(sIdx, { productionTime: Number(e.target.value) })}
                        min={1}
                      />
                    </Field>
                  </div>

                  <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
                    <Field label="End Product" tooltip="The item template produced by this craft. Search by name or paste a 24-char TPL.">
                      <SearchableSelect
                        value={scheme.endProduct}
                        onChange={v => updateScheme(sIdx, { endProduct: v })}
                        options={itemOptions}
                        placeholder="Search end product..."
                      />
                    </Field>
                    <Field label="Count" tooltip="How many of the end product are created each craft.">
                      <input
                        type="number"
                        className="input-field text-xs w-full"
                        value={scheme.count}
                        onChange={e => updateScheme(sIdx, { count: Number(e.target.value) })}
                        min={1}
                      />
                    </Field>
                    <div className="flex items-center gap-4 pt-5">
                      <Toggle
                        label="Fuel for full time"
                        tooltip="If checked, the hideout generator must be powered for the entire craft duration."
                        value={!!scheme.needFuelForAllProductionTime}
                        onChange={v => updateScheme(sIdx, { needFuelForAllProductionTime: v })}
                      />
                      <Toggle
                        label="Unlocked by default"
                        tooltip="If checked, the recipe is available immediately. Unchecked recipes must be unlocked by a quest reward."
                        value={!!scheme.unlockedByDefault}
                        onChange={v => updateScheme(sIdx, { unlockedByDefault: v })}
                      />
                    </div>
                  </div>

                  <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
                    <Field label="Production Limit" tooltip="Maximum number of crafts that can be queued. Use 0 for unlimited.">
                      <input
                        type="number"
                        className="input-field text-xs w-full"
                        value={scheme.productionLimitCount ?? 0}
                        onChange={e => updateScheme(sIdx, { productionLimitCount: Number(e.target.value) })}
                        min={0}
                      />
                    </Field>
                    <div className="flex items-center gap-4 pt-5">
                      <Toggle
                        label="Continuous"
                        tooltip="If checked, the craft runs continuously like water or fuel production."
                        value={!!scheme.continuous}
                        onChange={v => updateScheme(sIdx, { continuous: v })}
                      />
                    </div>
                  </div>

                  <div className="pt-2 border-t border-tarkov-border/30">
                    <div className="flex items-center justify-between mb-2">
                      <h4 className="text-xs font-semibold text-tarkov-text-dim">Requirements</h4>
                      <button
                        onClick={() => addRequirement(sIdx)}
                        className="btn-secondary text-xs flex items-center gap-1 px-2 py-0.5"
                      >
                        <Plus size={10} /> Add Requirement
                      </button>
                    </div>

                    {scheme.requirements.length === 0 && (
                      <p className="text-xs text-tarkov-text-dim">No requirements.</p>
                    )}

                    <div className="space-y-2">
                      {scheme.requirements.map((req, rIdx) => (
                        <div key={rIdx} className="flex flex-wrap gap-2 items-start bg-tarkov-bg rounded p-2 border border-tarkov-border/30">
                          <Field label="Type" tooltip="Item: consumed. Tool: must be owned but not consumed. Area: requires a hideout area level. Resource: consumes a hideout resource like water/fuel.">
                            <select
                              className="input-field text-xs"
                              value={req.type}
                              onChange={e => updateRequirement(sIdx, rIdx, { type: e.target.value as SchemeRequirement['type'] })}
                            >
                              <option value="Item">Item</option>
                              <option value="Tool">Tool</option>
                              <option value="Area">Area</option>
                              <option value="Resource">Resource</option>
                            </select>
                          </Field>

                          {req.type !== 'Area' && (
                            <div className="flex-1 min-w-[12rem]">
                              <Field label="Item" tooltip="The item template for this Item, Tool or Resource requirement.">
                                <SearchableSelect
                                  value={req.templateId || ''}
                                  onChange={v => updateRequirement(sIdx, rIdx, { templateId: v })}
                                  options={itemOptions}
                                  placeholder="Search item..."
                                />
                              </Field>
                            </div>
                          )}

                          {(req.type === 'Item' || req.type === 'Tool') && (
                            <Field label="Count" tooltip={req.type === 'Item' ? 'How many of this item are consumed.' : 'How many of this tool are required (usually 1).'}>
                              <input
                                type="number"
                                className="input-field text-xs w-20"
                                value={req.count || ''}
                                onChange={e => updateRequirement(sIdx, rIdx, { count: Number(e.target.value) })}
                                min={0}
                                placeholder="Count"
                              />
                            </Field>
                          )}

                          {req.type === 'Item' && (
                            <div className="flex flex-wrap gap-3 items-center">
                              <Toggle
                                label="Encoded"
                                tooltip="Whether this item is encoded (rare, mostly false)."
                                value={!!req.isEncoded}
                                onChange={v => updateRequirement(sIdx, rIdx, { isEncoded: v })}
                              />
                              <Toggle
                                label="Functional"
                                tooltip="Whether the item must be in functional condition."
                                value={!!req.isFunctional}
                                onChange={v => updateRequirement(sIdx, rIdx, { isFunctional: v })}
                              />
                              <Toggle
                                label="FiR"
                                tooltip="Whether the item must be found in raid."
                                value={!!req.isSpawnedInSession}
                                onChange={v => updateRequirement(sIdx, rIdx, { isSpawnedInSession: v })}
                              />
                            </div>
                          )}

                          {req.type === 'Area' && (
                            <>
                              <Field label="Area" tooltip="The hideout area required for this craft.">
                                <select
                                  className="input-field text-xs w-48"
                                  value={req.areaType ?? ''}
                                  onChange={e => updateRequirement(sIdx, rIdx, { areaType: Number(e.target.value) })}
                                >
                                  <option value="">Select area…</option>
                                  {HIDEOUT_AREAS.map(a => (
                                    <option key={a.value} value={a.value}>{a.value} — {a.label}</option>
                                  ))}
                                </select>
                              </Field>
                              <Field label="Level" tooltip="The required level of that hideout area.">
                                <input
                                  type="number"
                                  className="input-field text-xs w-24"
                                  value={req.requiredLevel ?? ''}
                                  onChange={e => updateRequirement(sIdx, rIdx, { requiredLevel: Number(e.target.value) })}
                                  placeholder="Level"
                                />
                              </Field>
                            </>
                          )}

                          {req.type === 'Resource' && (
                            <Field label="Amount" tooltip="How much of the resource is consumed.">
                              <input
                                type="number"
                                className="input-field text-xs w-28"
                                value={req.resource ?? ''}
                                onChange={e => updateRequirement(sIdx, rIdx, { resource: Number(e.target.value) })}
                                placeholder="Resource"
                              />
                            </Field>
                          )}

                          <button
                            onClick={() => removeRequirement(sIdx, rIdx)}
                            className="text-tarkov-error hover:text-tarkov-error/80 p-1 mt-0.5"
                          >
                            <Trash2 size={12} />
                          </button>
                        </div>
                      ))}
                    </div>
                  </div>
                </div>
              )}
            </div>
          )
        })}
      </div>
    </div>
  )
}
