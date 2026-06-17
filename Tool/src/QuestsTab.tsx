import { useState, useCallback, useMemo } from 'react'
import {
  Plus, Trash2, ChevronDown, ChevronUp, RefreshCw, Target, Crosshair,
  Clock, MapPin, HelpCircle, AlertCircle, Upload, Image as ImageIcon,
  Scroll, Repeat, GripVertical, Copy, Package,
} from 'lucide-react'
import type {
  QuestPackDefinition, StoryQuestDefinition, QuestObjective, QuestRewards,
  RotatingQuestTemplate, RotatingObjectiveTemplate, ValidationError,
} from './types'
import { useItemNames } from './useItemNames'
import {
  createDefaultStoryQuest, createDefaultObjective, createDefaultRotatingTemplate,
  createDefaultRotatingObjective, generateMongoId,
  MAP_LOCATIONS, OBJECTIVE_TYPES, ENEMY_TARGETS, ROTATION_TYPES,
} from './types'

// ==================== Shared sub-components ====================

function Field({ label, error, tooltip, children }: {
  label: string; error?: boolean; tooltip?: string; children: React.ReactNode
}) {
  return (
    <div>
      <label className={`label ${error ? 'text-tarkov-error' : ''} flex items-center gap-1.5`}>
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

function ImageDrop({ dataUrl, onDrop, label, hint }: {
  dataUrl?: string; onDrop: (url: string) => void; label: string; hint: string
}) {
  return (
    <div
      className={`relative border-2 border-dashed rounded-lg p-3 text-center cursor-pointer transition-colors
        ${dataUrl ? 'border-tarkov-accent/50 bg-tarkov-accent/5' : 'border-tarkov-border hover:border-tarkov-accent/40'}`}
      onDragOver={e => { e.preventDefault(); e.stopPropagation() }}
      onDrop={e => {
        e.preventDefault(); e.stopPropagation()
        const file = e.dataTransfer.files?.[0]
        if (file?.type.startsWith('image/')) {
          const reader = new FileReader()
          reader.onload = ev => onDrop(ev.target?.result as string)
          reader.readAsDataURL(file)
        }
      }}
      onClick={() => {
        const input = document.createElement('input')
        input.type = 'file'; input.accept = 'image/*'
        input.onchange = e => {
          const file = (e.target as HTMLInputElement).files?.[0]
          if (file) {
            const reader = new FileReader()
            reader.onload = ev => onDrop(ev.target?.result as string)
            reader.readAsDataURL(file)
          }
        }
        input.click()
      }}
    >
      {dataUrl ? (
        <div className="flex items-center gap-3">
          <img src={dataUrl} alt="Preview" className="w-12 h-12 rounded object-cover border border-tarkov-border" />
          <div className="text-left">
            <p className="text-sm text-tarkov-text">{label} loaded</p>
            <p className="text-xs text-tarkov-text-dim">Click or drag to replace</p>
          </div>
        </div>
      ) : (
        <div className="py-1">
          <Upload size={20} className="mx-auto text-tarkov-text-dim mb-1" />
          <p className="text-xs text-tarkov-text-dim">{hint}</p>
        </div>
      )}
    </div>
  )
}

// ==================== Main Quests Tab ====================

export default function QuestsTab({ questPack, traderId, onChange, errors }: {
  questPack: QuestPackDefinition
  traderId: string
  onChange: (pack: QuestPackDefinition) => void
  errors: ValidationError[]
}) {
  const [activeSection, setActiveSection] = useState<'story' | 'rotating'>('story')
  const [expandedQuest, setExpandedQuest] = useState<number | null>(null)
  const [expandedRotating, setExpandedRotating] = useState<number | null>(null)

  const hasQuests = questPack.storyQuests.length > 0 || questPack.rotatingQuests.length > 0

  // ---- Story quest CRUD ----
  const addStoryQuest = useCallback(() => {
    const newQ = createDefaultStoryQuest(traderId)
    const updated = { ...questPack, storyQuests: [...questPack.storyQuests, newQ] }
    onChange(updated)
    setExpandedQuest(questPack.storyQuests.length)
  }, [questPack, traderId, onChange])

  const removeStoryQuest = useCallback((idx: number) => {
    onChange({ ...questPack, storyQuests: questPack.storyQuests.filter((_, i) => i !== idx) })
    if (expandedQuest === idx) setExpandedQuest(null)
  }, [questPack, onChange, expandedQuest])

  const updateStoryQuest = useCallback((idx: number, updates: Partial<StoryQuestDefinition>) => {
    onChange({
      ...questPack,
      storyQuests: questPack.storyQuests.map((q, i) => i === idx ? { ...q, ...updates } : q),
    })
  }, [questPack, onChange])

  const duplicateStoryQuest = useCallback((idx: number) => {
    const src = questPack.storyQuests[idx]
    const dup = { ...src, id: generateMongoId(), name: src.name + ' (copy)' }
    const updated = [...questPack.storyQuests]
    updated.splice(idx + 1, 0, dup)
    onChange({ ...questPack, storyQuests: updated })
    setExpandedQuest(idx + 1)
  }, [questPack, onChange])

  // ---- Rotating template CRUD ----
  const addRotating = useCallback(() => {
    const newT = createDefaultRotatingTemplate()
    onChange({ ...questPack, rotatingQuests: [...questPack.rotatingQuests, newT] })
    setExpandedRotating(questPack.rotatingQuests.length)
  }, [questPack, onChange])

  const removeRotating = useCallback((idx: number) => {
    onChange({ ...questPack, rotatingQuests: questPack.rotatingQuests.filter((_, i) => i !== idx) })
    if (expandedRotating === idx) setExpandedRotating(null)
  }, [questPack, onChange, expandedRotating])

  const updateRotating = useCallback((idx: number, updates: Partial<RotatingQuestTemplate>) => {
    onChange({
      ...questPack,
      rotatingQuests: questPack.rotatingQuests.map((t, i) => i === idx ? { ...t, ...updates } : t),
    })
  }, [questPack, onChange])

  return (
    <div className="space-y-6">
      {/* Default Quest Icon */}
      <section className="card">
        <h2 className="text-lg font-semibold text-tarkov-accent mb-3 flex items-center gap-2">
          <ImageIcon size={18} /> Default Quest Icon
        </h2>
        <p className="text-sm text-tarkov-text-dim mb-3">
          This icon is used for all quests that don't have their own icon. Drop a PNG image (recommended: 332×332 px).
        </p>
        <div className="max-w-sm">
          <ImageDrop
            dataUrl={questPack.defaultQuestIconDataUrl}
            onDrop={url => onChange({ ...questPack, defaultQuestIconDataUrl: url, defaultQuestIcon: 'assets/default_quest_icon.png' })}
            label="Default icon"
            hint="Drag & drop or click to set default quest icon"
          />
        </div>
      </section>

      {/* Section Toggle */}
      <div className="flex gap-1 bg-tarkov-surface border border-tarkov-border rounded-lg p-1">
        <button
          onClick={() => setActiveSection('story')}
          className={`flex-1 flex items-center justify-center gap-2 px-4 py-2 rounded text-sm font-medium transition-colors ${
            activeSection === 'story' ? 'bg-tarkov-accent text-tarkov-bg' : 'text-tarkov-text-dim hover:text-tarkov-text'
          }`}
        >
          <Scroll size={16} /> Story Quests
          {questPack.storyQuests.length > 0 && (
            <span className={`ml-1 text-xs px-1.5 py-0.5 rounded-full ${
              activeSection === 'story' ? 'bg-tarkov-bg/30 text-tarkov-bg' : 'bg-tarkov-accent/20 text-tarkov-accent'
            }`}>{questPack.storyQuests.length}</span>
          )}
        </button>
        <button
          disabled
          className="flex-1 flex items-center justify-center gap-2 px-4 py-2 rounded text-sm font-medium opacity-50 cursor-not-allowed bg-tarkov-bg/50 text-tarkov-text-dim"
        >
          <Repeat size={16} /> Rotating Templates
          <span className="ml-1 text-xs px-1.5 py-0.5 rounded-full bg-tarkov-accent/20 text-tarkov-accent">WIP</span>
          {questPack.rotatingQuests.length > 0 && (
            <span className="ml-1 text-xs px-1.5 py-0.5 rounded-full bg-tarkov-accent/20 text-tarkov-accent">{questPack.rotatingQuests.length}</span>
          )}
        </button>
      </div>

      {/* Story Quests Section */}
      {activeSection === 'story' && (
        <div className="space-y-4">
          <div className="flex items-center justify-between">
            <p className="text-sm text-tarkov-text-dim">
              Fixed quests with specific objectives, rewards, and optional chaining.
            </p>
            <button onClick={addStoryQuest} className="btn-primary text-sm flex items-center gap-1.5">
              <Plus size={14} /> Add Quest
            </button>
          </div>

          {questPack.storyQuests.length === 0 && (
            <div className="card text-center text-tarkov-text-dim py-8">
              No story quests defined. Add one to get started.
            </div>
          )}

          <div className="space-y-2">
            {questPack.storyQuests.map((quest, qi) => {
              const isExpanded = expandedQuest === qi
              const questErrors = errors.filter(e => e.field.startsWith(`quest.${qi}`))

              return (
                <div key={quest.id} className={`card ${questErrors.length > 0 ? 'border-tarkov-error/50' : ''}`}>
                  {/* Header */}
                  <div className="flex items-center justify-between cursor-pointer" onClick={() => setExpandedQuest(isExpanded ? null : qi)}>
                    <div className="flex items-center gap-3">
                      <GripVertical size={14} className="text-tarkov-text-dim" />
                      {isExpanded ? <ChevronUp size={16} /> : <ChevronDown size={16} />}
                      <Crosshair size={14} className="text-tarkov-accent" />
                      <span className="font-medium text-tarkov-text">
                        {quest.name || '(unnamed quest)'}
                      </span>
                      <span className="text-xs bg-tarkov-accent/20 text-tarkov-accent px-2 py-0.5 rounded">
                        {quest.objectives.length} obj
                      </span>
                      <span className="text-xs text-tarkov-text-dim font-mono">
                        {MAP_LOCATIONS.find(l => l.value === quest.location)?.label || quest.location}
                      </span>
                      {questErrors.length > 0 && <AlertCircle size={14} className="text-tarkov-error" />}
                    </div>
                    <div className="flex items-center gap-1">
                      <button onClick={e => { e.stopPropagation(); duplicateStoryQuest(qi) }}
                        className="text-tarkov-text-dim hover:text-tarkov-accent transition-colors p-1" title="Duplicate">
                        <Copy size={14} />
                      </button>
                      <button onClick={e => { e.stopPropagation(); removeStoryQuest(qi) }}
                        className="text-tarkov-error hover:text-tarkov-error/80 transition-colors p-1" title="Delete">
                        <Trash2 size={14} />
                      </button>
                    </div>
                  </div>

                  {/* Expanded editor */}
                  {isExpanded && (
                    <StoryQuestEditor
                      quest={quest}
                      questIndex={qi}
                      allQuests={questPack.storyQuests}
                      onChange={updates => updateStoryQuest(qi, updates)}
                      errors={questErrors}
                    />
                  )}
                </div>
              )
            })}
          </div>
        </div>
      )}

      {/* Rotating Templates Section */}
      {activeSection === 'rotating' && (
        <div className="space-y-4">
          <div className="flex items-center justify-between">
            <p className="text-sm text-tarkov-text-dim">
              Templates that generate random quests at server start. Use <code className="text-tarkov-accent">{'{location}'}</code> in name/description for auto-fill.
            </p>
            <button onClick={addRotating} className="btn-primary text-sm flex items-center gap-1.5">
              <Plus size={14} /> Add Template
            </button>
          </div>

          {questPack.rotatingQuests.length === 0 && (
            <div className="card text-center text-tarkov-text-dim py-8">
              No rotating quest templates. Add one for auto-generated daily/weekly quests.
            </div>
          )}

          <div className="space-y-2">
            {questPack.rotatingQuests.map((tmpl, ti) => {
              const isExpanded = expandedRotating === ti
              const tmplErrors = errors.filter(e => e.field.startsWith(`rotating.${ti}`))

              return (
                <div key={tmpl.id} className={`card ${tmplErrors.length > 0 ? 'border-tarkov-error/50' : ''}`}>
                  <div className="flex items-center justify-between cursor-pointer" onClick={() => setExpandedRotating(isExpanded ? null : ti)}>
                    <div className="flex items-center gap-3">
                      {isExpanded ? <ChevronUp size={16} /> : <ChevronDown size={16} />}
                      <Repeat size={14} className="text-tarkov-accent" />
                      <span className="font-medium text-tarkov-text">
                        {tmpl.namePool[0] || '(unnamed template)'}
                      </span>
                      <span className={`text-xs px-2 py-0.5 rounded ${
                        tmpl.rotation === 'daily' ? 'bg-blue-500/20 text-blue-400' : 'bg-purple-500/20 text-purple-400'
                      }`}>{tmpl.rotation}</span>
                      <span className="text-xs text-tarkov-text-dim">
                        {tmpl.objectives.length} objectives
                      </span>
                      {tmplErrors.length > 0 && <AlertCircle size={14} className="text-tarkov-error" />}
                    </div>
                    <button onClick={e => { e.stopPropagation(); removeRotating(ti) }}
                      className="text-tarkov-error hover:text-tarkov-error/80 transition-colors p-1">
                      <Trash2 size={14} />
                    </button>
                  </div>

                  {isExpanded && (
                    <RotatingTemplateEditor
                      template={tmpl}
                      onChange={updates => updateRotating(ti, updates)}
                      errors={tmplErrors}
                    />
                  )}
                </div>
              )
            })}
          </div>
        </div>
      )}

      {!hasQuests && (
        <div className="bg-tarkov-surface border border-tarkov-border rounded-lg px-4 py-3 text-sm text-tarkov-text-dim flex items-center gap-2">
          <HelpCircle size={14} className="text-tarkov-accent shrink-0" />
          No quests defined — no quests.json will be included in the export. Add story quests or rotating templates above if you want quests for this trader.
        </div>
      )}
    </div>
  )
}

// ==================== Story Quest Editor ====================

function StoryQuestEditor({ quest, questIndex, allQuests, onChange, errors }: {
  quest: StoryQuestDefinition
  questIndex: number
  allQuests: StoryQuestDefinition[]
  onChange: (updates: Partial<StoryQuestDefinition>) => void
  errors: ValidationError[]
}) {
  const [expandedObj, setExpandedObj] = useState<number | null>(null)

  const itemTpls = useMemo(
    () => [
      ...quest.objectives.map(o => o.itemTpl).filter(Boolean),
      ...(quest.rewards.items || []).map(i => i.itemTpl).filter(Boolean)
    ] as string[],
    [quest.objectives.map(o => o.itemTpl).join(','), quest.rewards.items?.map(i => i.itemTpl).join(',')]
  )
  const itemNames = useItemNames(itemTpls)

  const addObjective = () => {
    onChange({ objectives: [...quest.objectives, createDefaultObjective()] })
    setExpandedObj(quest.objectives.length)
  }

  const removeObjective = (idx: number) => {
    onChange({ objectives: quest.objectives.filter((_, i) => i !== idx) })
    if (expandedObj === idx) setExpandedObj(null)
  }

  const updateObjective = (idx: number, updates: Partial<QuestObjective>) => {
    onChange({ objectives: quest.objectives.map((o, i) => i === idx ? { ...o, ...updates } : o) })
  }

  const updateRewards = (updates: Partial<QuestRewards>) => {
    onChange({ rewards: { ...quest.rewards, ...updates } })
  }

  const otherQuests = allQuests.filter((_, i) => i !== questIndex)

  return (
    <div className="mt-4 pt-4 border-t border-tarkov-border space-y-5">
      {/* Basic Info */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        <Field label="Quest ID" tooltip="Unique 24-character hex ID for this quest. Auto-generated.">
          <div className="flex gap-2">
            <input className="input-field flex-1 font-mono text-sm" value={quest.id}
              onChange={e => onChange({ id: e.target.value })} maxLength={24} />
            <button onClick={() => onChange({ id: generateMongoId() })} className="btn-secondary text-xs px-2" title="Regenerate ID">
              <RefreshCw size={14} />
            </button>
          </div>
        </Field>

        <Field label="Quest Name" tooltip="Display name shown in the quest log.">
          <input className="input-field" value={quest.name}
            onChange={e => onChange({ name: e.target.value })} placeholder="e.g. First Impressions" />
        </Field>
      </div>

      <Field label="Description" tooltip="Quest description shown when the player views the quest.">
        <textarea className="input-field min-h-[60px] resize-y" value={quest.description}
          onChange={e => onChange({ description: e.target.value })}
          placeholder="Describe what the player needs to do and why..." />
      </Field>

      {/* Check if objectives span multiple maps */}
      {(() => {
        const objectiveLocations = quest.objectives
          .map(o => o.location)
          .filter((loc): loc is string => !!loc && loc !== 'any')
        const uniqueLocations = [...new Set(objectiveLocations)]
        const needsAnyLocation = uniqueLocations.length > 1 && quest.location !== 'any'

        return needsAnyLocation ? (
          <div className="bg-tarkov-accent/10 border border-tarkov-accent/30 rounded-lg p-3 mb-4">
            <div className="flex items-start gap-2">
              <AlertCircle size={16} className="text-tarkov-accent mt-0.5 shrink-0" />
              <div className="text-sm text-tarkov-text">
                <p className="font-medium text-tarkov-accent">Multi-Map Quest Detected</p>
                <p className="text-tarkov-text-dim mt-1">
                  This quest has objectives on different maps ({uniqueLocations.map(l => MAP_LOCATIONS.find(ml => ml.value === l)?.label || l).join(', ')}).
                  Set Location to "Any Location" so the quest appears on all maps. Each objective will still require its specific map.
                </p>
              </div>
            </div>
          </div>
        ) : null
      })()}

      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        <Field label="Location" tooltip="Map restriction. 'Any Location' means no map lock.">
          <select className="input-field" value={quest.location}
            onChange={e => onChange({ location: e.target.value })}>
            {MAP_LOCATIONS.map(l => <option key={l.value} value={l.value}>{l.label}</option>)}
          </select>
        </Field>

        <Field label="Required Level" tooltip="Minimum player level to unlock this quest.">
          <input type="number" className="input-field" value={quest.requirements.playerLevel}
            onChange={e => onChange({ requirements: { ...quest.requirements, playerLevel: Number(e.target.value) } })}
            min={1} max={79} />
        </Field>

        <Field label="Previous Quest" tooltip="Select a quest that must be completed first, or leave as 'None' for no prerequisite. Choose 'Other' to enter an external quest ID.">
          <select className="input-field" value={
            !quest.requirements.previousQuest ? '' :
            otherQuests.some(q => q.id === quest.requirements.previousQuest) ? quest.requirements.previousQuest :
            '__other__'
          }
            onChange={e => {
              const val = e.target.value
              if (val === '__other__') {
                onChange({ requirements: { ...quest.requirements, previousQuest: '' } })
              } else {
                onChange({ requirements: { ...quest.requirements, previousQuest: val || undefined } })
              }
            }}>
            <option value="">None (no prerequisite)</option>
            {otherQuests.map(q => (
              <option key={q.id} value={q.id}>{q.name || q.id}</option>
            ))}
            <option value="__other__">Other (external quest ID)</option>
          </select>
          {quest.requirements.previousQuest !== undefined && !otherQuests.some(q => q.id === quest.requirements.previousQuest) && (
            <input className="input-field font-mono text-sm mt-1" value={quest.requirements.previousQuest || ''}
              onChange={e => onChange({ requirements: { ...quest.requirements, previousQuest: e.target.value || undefined } })}
              placeholder="Enter a 24-char quest ID from another mod or vanilla" maxLength={24} />
          )}
        </Field>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        <Field label="Started Message" tooltip="Message shown when the quest becomes active.">
          <input className="input-field" value={quest.startedMessage}
            onChange={e => onChange({ startedMessage: e.target.value })} />
        </Field>
        <Field label="Success Message" tooltip="Message shown when the quest is completed.">
          <input className="input-field" value={quest.successMessage}
            onChange={e => onChange({ successMessage: e.target.value })} />
        </Field>
      </div>

      {/* Per-quest icon */}
      <div className="max-w-sm">
        <Field label="Quest Icon (optional)" tooltip="Override the default icon for this specific quest. Drag a PNG image.">
          <ImageDrop
            dataUrl={quest.imageDataUrl}
            onDrop={url => onChange({ imageDataUrl: url, image: `assets/quest_${quest.id}.png` })}
            label="Quest icon"
            hint="Drop an icon to override the default"
          />
        </Field>
      </div>

      {/* Objectives */}
      <div>
        <div className="flex items-center justify-between mb-3">
          <h3 className="text-sm font-semibold text-tarkov-accent flex items-center gap-2">
            <Target size={16} /> Objectives ({quest.objectives.length})
          </h3>
          <button onClick={addObjective} className="btn-secondary text-xs flex items-center gap-1.5">
            <Plus size={12} /> Add Objective
          </button>
        </div>

        {quest.objectives.length === 0 && (
          <div className="bg-tarkov-bg rounded-lg p-4 text-center text-tarkov-text-dim text-sm">
            No objectives. Add at least one.
          </div>
        )}

        <div className="space-y-2">
          {quest.objectives.map((obj, oi) => {
            const isExp = expandedObj === oi
            const typeLabel = OBJECTIVE_TYPES.find(t => t.value === obj.type)?.label || obj.type
            const objLocation = obj.location
            const questLocation = quest.location
            const locationMismatch = objLocation && questLocation !== 'any' && objLocation !== questLocation

            return (
              <div key={oi} className={`bg-tarkov-bg rounded-lg border ${locationMismatch ? 'border-tarkov-error/50' : 'border-tarkov-border'}`}>
                <div className="flex items-center justify-between px-3 py-2 cursor-pointer"
                  onClick={() => setExpandedObj(isExp ? null : oi)}>
                  <div className="flex items-center gap-2">
                    {isExp ? <ChevronUp size={14} /> : <ChevronDown size={14} />}
                    <span className="text-sm text-tarkov-text">{typeLabel}</span>
                    <span className="text-xs text-tarkov-text-dim">×{obj.count}</span>
                    {obj.itemTpl && itemNames.get(obj.itemTpl) && (
                      <span className="text-xs text-tarkov-text italic">{itemNames.get(obj.itemTpl)}</span>
                    )}
                    {obj.location && (
                      <span className="text-xs text-tarkov-text-dim">
                        on {MAP_LOCATIONS.find(l => l.value === obj.location)?.label || obj.location}
                      </span>
                    )}
                    {locationMismatch && (
                      <span className="text-xs bg-tarkov-error/20 text-tarkov-error px-1.5 py-0.5 rounded flex items-center gap-1">
                        <AlertCircle size={10} /> Location mismatch
                      </span>
                    )}
                  </div>
                  <button onClick={e => { e.stopPropagation(); removeObjective(oi) }}
                    className="text-tarkov-error hover:text-tarkov-error/80 p-1">
                    <Trash2 size={12} />
                  </button>
                </div>

                {isExp && (
                  <ObjectiveEditor
                    objective={obj}
                    onChange={updates => updateObjective(oi, updates)}
                  />
                )}
              </div>
            )
          })}
        </div>
      </div>

      {/* Rewards */}
      <div>
        <h3 className="text-sm font-semibold text-tarkov-accent flex items-center gap-2 mb-3">
          <Star size={16} /> Rewards
        </h3>
        <div className="bg-tarkov-bg rounded-lg p-4 space-y-4">
          <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
            <Field label="XP" tooltip="Experience points awarded on completion.">
              <input type="number" className="input-field" value={quest.rewards.xp}
                onChange={e => updateRewards({ xp: Number(e.target.value) })} min={0} />
            </Field>
            <Field label="Money Amount" tooltip="Amount of currency given as reward.">
              <input type="number" className="input-field" value={quest.rewards.money?.amount || 0}
                onChange={e => updateRewards({
                  money: { currency: quest.rewards.money?.currency || 'RUB', amount: Number(e.target.value) }
                })} min={0} />
            </Field>
            <Field label="Currency" tooltip="Which currency for the money reward.">
              <select className="input-field" value={quest.rewards.money?.currency || 'RUB'}
                onChange={e => updateRewards({
                  money: { amount: quest.rewards.money?.amount || 0, currency: e.target.value }
                })}>
                <option value="RUB">Roubles</option>
                <option value="USD">Dollars</option>
                <option value="EUR">Euros</option>
              </select>
            </Field>
            <Field label="Standing" tooltip="Trader reputation gained. Use small values like 0.01-0.05.">
              <input type="number" className="input-field" value={quest.rewards.traderStanding}
                onChange={e => updateRewards({ traderStanding: Number(e.target.value) })}
                step={0.01} min={-1} max={1} />
            </Field>
          </div>

          {/* Item Rewards - compact inline */}
          <div className="mt-3 flex items-center gap-3 flex-wrap">
            <button
              onClick={() => {
                const newItems = [...(quest.rewards.items || []), { itemTpl: '', count: 1 }]
                updateRewards({ items: newItems })
              }}
              className="btn-secondary text-xs flex items-center gap-1 px-2 py-1"
            >
              <Plus size={12} /> Add Item
            </button>
            <p className="text-xs text-tarkov-text-dim">
              Find IDs at{' '}
              <a href="https://db.sp-tarkov.com/search" target="_blank" rel="noopener noreferrer"
                className="text-tarkov-accent hover:text-tarkov-accent-hover underline">db.sp-tarkov.com</a>
            </p>
            {(quest.rewards.items || []).length > 0 && (
              <span className="text-xs text-tarkov-text-dim">{(quest.rewards.items || []).length} item(s)</span>
            )}
          </div>

          {(quest.rewards.items || []).length > 0 && (
            <div className="mt-2 space-y-1">
              {(quest.rewards.items || []).map((item, idx) => (
                <div key={idx} className="flex items-start gap-2 bg-tarkov-bg rounded p-1.5 border border-tarkov-border/50">
                  <div className="flex-1 min-w-0 max-w-[240px]">
                    <div className="flex items-center gap-1">
                      <input
                        type="text"
                        className="input-field text-xs font-mono w-full py-1"
                        value={item.itemTpl}
                        onChange={e => {
                          const newItems = [...(quest.rewards.items || [])]
                          newItems[idx] = { ...item, itemTpl: e.target.value }
                          updateRewards({ items: newItems })
                        }}
                        placeholder="Item TPL ID"
                        maxLength={24}
                      />
                      <TooltipIcon text="Item to be rewarded." />
                    </div>
                    {item.itemTpl && itemNames.get(item.itemTpl) && (
                      <p className="text-xs text-tarkov-accent truncate">{itemNames.get(item.itemTpl)}</p>
                    )}
                  </div>
                  <div className="w-20">
                    <div className="flex items-center gap-1">
                      <input
                        type="number"
                        min="1"
                        className="input-field text-xs w-full text-center py-1"
                        value={item.count}
                        onChange={e => {
                          const newItems = [...(quest.rewards.items || [])]
                          newItems[idx] = { ...item, count: parseInt(e.target.value) || 1 }
                          updateRewards({ items: newItems })
                        }}
                      />
                      <TooltipIcon text="Quantity of this item to be rewarded." />
                    </div>
                  </div>
                  <button
                    onClick={() => {
                      const newItems = (quest.rewards.items || []).filter((_, i) => i !== idx)
                      updateRewards({ items: newItems })
                    }}
                    className="text-tarkov-error hover:text-tarkov-error/80 p-1 mt-1"
                  >
                    <Trash2 size={12} />
                  </button>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>

      {/* Errors */}
      {errors.length > 0 && (
        <div className="text-sm text-tarkov-error space-y-1">
          {errors.map((e, i) => (
            <div key={i} className="flex items-center gap-1.5">
              <AlertCircle size={12} /> {e.message}
            </div>
          ))}
        </div>
      )}
    </div>
  )
}

// ==================== Objective Editor ====================

function ObjectiveEditor({ objective, onChange }: {
  objective: QuestObjective
  onChange: (updates: Partial<QuestObjective>) => void
}) {
  const isKill = objective.type === 'kill_enemy'
  const isHandover = objective.type === 'handover_item' || objective.type === 'handover_fir_item'
  const isLocation = objective.type === 'survive_location' || objective.type === 'extract_location'

  return (
    <div className="px-3 pb-3 space-y-3 border-t border-tarkov-border/50">
      <div className="grid grid-cols-2 md:grid-cols-4 gap-3 pt-3">
        <Field label="Type" tooltip="What kind of objective this is.">
          <select className="input-field text-sm" value={objective.type}
            onChange={e => {
              const t = e.target.value
              const updates: Partial<QuestObjective> = { type: t }
              if (t === 'kill_enemy') { updates.target = 'Savage'; updates.itemTpl = undefined }
              if (t === 'handover_item' || t === 'handover_fir_item') { updates.target = undefined; updates.itemTpl = ''; updates.location = undefined }
              if (t === 'survive_location' || t === 'extract_location') { updates.location = 'bigmap'; updates.target = undefined; updates.itemTpl = undefined }
              onChange(updates)
            }}>
            {OBJECTIVE_TYPES.map(t => <option key={t.value} value={t.value}>{t.label}</option>)}
          </select>
        </Field>

        <Field label="Count" tooltip="How many times this objective must be completed.">
          <input type="number" className="input-field text-sm" value={objective.count}
            onChange={e => onChange({ count: Number(e.target.value) })} min={1} />
        </Field>

        {isKill && (
          <Field label="Enemy Target" tooltip="What type of enemy to kill.">
            <select className="input-field text-sm" value={objective.target || 'Savage'}
              onChange={e => onChange({ target: e.target.value })}>
              {ENEMY_TARGETS.map(t => <option key={t.value} value={t.value}>{t.label}</option>)}
            </select>
          </Field>
        )}

        {isKill && (
          <Field label="Location (optional)" tooltip="Restrict kills to a specific map. Leave empty for any map.">
            <select className="input-field text-sm" value={objective.location || ''}
              onChange={e => onChange({ location: e.target.value || undefined })}>
              <option value="">Any Map</option>
              {MAP_LOCATIONS.filter(l => l.value !== 'any').map(l => (
                <option key={l.value} value={l.value}>{l.label}</option>
              ))}
            </select>
          </Field>
        )}

        {isHandover && (
          <Field label="Item Template ID" tooltip="The 24-char hex ID of the item to hand over. Find IDs at db.sp-tarkov.com/search">
            <input className="input-field text-sm font-mono" value={objective.itemTpl || ''}
              onChange={e => onChange({ itemTpl: e.target.value })} placeholder="24-char hex" maxLength={24} />
            <p className="text-xs text-tarkov-text-dim mt-1">
              Find IDs at{' '}
              <a href="https://db.sp-tarkov.com/search" target="_blank" rel="noopener noreferrer"
                className="text-tarkov-accent hover:text-tarkov-accent-hover underline">db.sp-tarkov.com</a>
            </p>
          </Field>
        )}

        {isLocation && (
          <Field label="Location" tooltip="The map to survive/extract from.">
            <select className="input-field text-sm" value={objective.location || 'bigmap'}
              onChange={e => onChange({ location: e.target.value })}>
              {MAP_LOCATIONS.filter(l => l.value !== 'any').map(l => (
                <option key={l.value} value={l.value}>{l.label}</option>
              ))}
            </select>
          </Field>
        )}
      </div>

      <Field label="Custom Description (optional)" tooltip="Override the auto-generated objective text with your own.">
        <input className="input-field text-sm" value={objective.description || ''}
          onChange={e => onChange({ description: e.target.value || undefined })}
          placeholder="Leave blank for auto-generated text" />
      </Field>
    </div>
  )
}

// ==================== Rotating Template Editor ====================

function RotatingTemplateEditor({ template, onChange, errors }: {
  template: RotatingQuestTemplate
  onChange: (updates: Partial<RotatingQuestTemplate>) => void
  errors: ValidationError[]
}) {
  const allItemTpls = useMemo(
    () => [...new Set(template.objectives.flatMap(o => o.itemPool).filter(Boolean))],
    [template.objectives.flatMap(o => o.itemPool).join(',')]
  )
  const itemNames = useItemNames(allItemTpls)

  const addObjective = () => {
    onChange({ objectives: [...template.objectives, createDefaultRotatingObjective()] })
  }

  const removeObjective = (idx: number) => {
    onChange({ objectives: template.objectives.filter((_, i) => i !== idx) })
  }

  const updateObjective = (idx: number, updates: Partial<RotatingObjectiveTemplate>) => {
    onChange({
      objectives: template.objectives.map((o, i) => i === idx ? { ...o, ...updates } : o),
    })
  }

  const toggleObjLocation = (objIdx: number, loc: string) => {
    const obj = template.objectives[objIdx]
    const current = obj.locationPool
    const updated = current.includes(loc)
      ? current.filter(l => l !== loc)
      : [...current, loc]
    updateObjective(objIdx, { locationPool: updated })
  }

  const toggleObjTarget = (objIdx: number, target: string) => {
    const obj = template.objectives[objIdx]
    const current = obj.targetPool
    const updated = current.includes(target)
      ? current.filter(t => t !== target)
      : [...current, target]
    updateObjective(objIdx, { targetPool: updated })
  }

  return (
    <div className="mt-4 pt-4 border-t border-tarkov-border space-y-5">
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <Field label="Template ID" tooltip="Unique 24-char hex ID for this template.">
          <div className="flex gap-2">
            <input className="input-field flex-1 font-mono text-sm" value={template.id}
              onChange={e => onChange({ id: e.target.value })} maxLength={24} />
            <button onClick={() => onChange({ id: generateMongoId() })} className="btn-secondary text-xs px-2">
              <RefreshCw size={14} />
            </button>
          </div>
        </Field>

        <Field label="Rotation" tooltip="Daily quests reset every 24h. Weekly quests reset every 7 days.">
          <select className="input-field" value={template.rotation}
            onChange={e => onChange({ rotation: e.target.value })}>
            {ROTATION_TYPES.map(r => <option key={r.value} value={r.value}>{r.label}</option>)}
          </select>
        </Field>

        <Field label="Quest Count" tooltip="How many quests to generate from this template.">
          <input type="number" className="input-field text-sm" value={template.questCount}
            onChange={e => onChange({ questCount: Math.max(1, Number(e.target.value)) })} min={1} />
        </Field>
      </div>

      {/* Name Pool */}
      <Field label="Name Pool" tooltip="One name per line. Use {location} as placeholder. A random name is picked for each generated quest.">
        <textarea className="input-field min-h-[60px] resize-y text-sm" value={template.namePool.join('\n')}
          onChange={e => onChange({ namePool: e.target.value.split('\n').filter(s => s.trim()) })}
          placeholder="Cleanup {location}\nHunt on {location}\nPatrol {location}" />
      </Field>

      {/* Description Pool */}
      <Field label="Description Pool" tooltip="One description per line. Use {location} placeholder. A random description is picked.">
        <textarea className="input-field min-h-[50px] resize-y text-sm" value={template.descriptionPool.join('\n')}
          onChange={e => onChange({ descriptionPool: e.target.value.split('\n').filter(s => s.trim()) })}
          placeholder="Head to {location} and deal with the threat." />
      </Field>

      {/* Template Icon */}
      <div className="max-w-sm">
        <Field label="Quest Icon (optional)" tooltip="Custom icon for quests generated from this template. Drop a JPG/PNG image.">
          <ImageDrop
            dataUrl={template.imageDataUrl}
            onDrop={url => onChange({ imageDataUrl: url, image: `assets/tpl_${template.id}.jpg` })}
            label="Template icon"
            hint="Drop an icon for this template's quests"
          />
        </Field>
      </div>

      {/* Objectives */}
      <div>
        <div className="flex items-center justify-between mb-3">
          <h4 className="text-sm font-semibold text-tarkov-text-dim flex items-center gap-2">
            <Target size={14} /> Objectives ({template.objectives.length})
          </h4>
          <button onClick={addObjective} className="btn-secondary text-xs flex items-center gap-1.5">
            <Plus size={12} /> Add
          </button>
        </div>

        <div className="space-y-3">
          {template.objectives.map((obj, oi) => (
            <div key={oi} className="bg-tarkov-bg rounded-lg border border-tarkov-border p-3 space-y-3">
              <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
                <Field label="Type">
                  <select className="input-field text-sm" value={obj.type}
                    onChange={e => {
                      const t = e.target.value
                      const updates: Partial<RotatingObjectiveTemplate> = { type: t }
                      if (t === 'kill_enemy') updates.targetPool = ['Savage']
                      else updates.targetPool = []
                      if (t === 'handover_item' || t === 'handover_fir_item') updates.itemPool = []
                      updateObjective(oi, updates)
                    }}>
                    {OBJECTIVE_TYPES.map(t => <option key={t.value} value={t.value}>{t.label}</option>)}
                  </select>
                </Field>
                <Field label="Min Count">
                  <input type="number" className="input-field text-sm" value={obj.countRange.min}
                    onChange={e => updateObjective(oi, { countRange: { ...obj.countRange, min: Number(e.target.value) } })} min={1} />
                </Field>
                <Field label="Max Count">
                  <input type="number" className="input-field text-sm" value={obj.countRange.max}
                    onChange={e => updateObjective(oi, { countRange: { ...obj.countRange, max: Number(e.target.value) } })} min={1} />
                </Field>
                <div className="flex items-end">
                  <button onClick={() => removeObjective(oi)}
                    className="text-tarkov-error hover:text-tarkov-error/80 mb-2">
                    <Trash2 size={14} />
                  </button>
                </div>
              </div>

              {/* Per-objective target pool for kill_enemy */}
              {obj.type === 'kill_enemy' && (
                <div>
                  <label className="label text-xs mb-1">Target Pool (select targets)</label>
                  <div className="flex flex-wrap gap-1.5">
                    {ENEMY_TARGETS.map(t => {
                      const active = obj.targetPool.includes(t.value)
                      return (
                        <button key={t.value}
                          onClick={() => toggleObjTarget(oi, t.value)}
                          className={`text-xs px-2 py-1 rounded border transition-colors ${
                            active
                              ? 'bg-tarkov-accent/20 border-tarkov-accent/50 text-tarkov-accent'
                              : 'bg-tarkov-bg border-tarkov-border text-tarkov-text-dim hover:border-tarkov-accent/30'
                          }`}>
                          {t.label}
                        </button>
                      )
                    })}
                  </div>
                </div>
              )}

              {/* Per-objective location pool */}
              <div>
                <label className="label text-xs mb-1 flex items-center gap-1">
                  <MapPin size={11} /> Location Pool
                </label>
                <div className="flex flex-wrap gap-1.5">
                  {MAP_LOCATIONS.filter(l => l.value !== 'any').map(loc => {
                    const active = obj.locationPool.includes(loc.value)
                    return (
                      <button key={loc.value}
                        onClick={() => toggleObjLocation(oi, loc.value)}
                        className={`text-xs px-2 py-1 rounded border transition-colors ${
                          active
                            ? 'bg-tarkov-accent/20 border-tarkov-accent/50 text-tarkov-accent'
                            : 'bg-tarkov-bg border-tarkov-border text-tarkov-text-dim hover:border-tarkov-accent/30'
                        }`}>
                        {loc.label}
                      </button>
                    )
                  })}
                </div>
              </div>

              {/* Per-objective item pool for handover types */}
              {(obj.type === 'handover_item' || obj.type === 'handover_fir_item') && (
                <Field label="Item Pool (template IDs, one per line)" tooltip="24-char hex item template IDs. A random item is picked for each generated quest. Find IDs at db.sp-tarkov.com/search">
                  <textarea className="input-field min-h-[40px] resize-y text-xs font-mono" value={obj.itemPool.join('\n')}
                    onChange={e => updateObjective(oi, { itemPool: e.target.value.split('\n').filter(s => s.trim()) })}
                    placeholder="5449016a4bdc2d6f028b456f" />
                  {obj.itemPool.some(id => itemNames.get(id)) && (
                    <div className="mt-1 space-y-0.5">
                      {obj.itemPool.map(id => itemNames.get(id) ? (
                        <p key={id} className="text-xs text-tarkov-text-dim font-mono">
                          <span className="text-tarkov-text-dim">{id}</span>
                          <span className="text-tarkov-text italic ml-2">{itemNames.get(id)}</span>
                        </p>
                      ) : null)}
                    </div>
                  )}
                  <p className="text-xs text-tarkov-text-dim mt-1">
                    Find item IDs at{' '}
                    <a href="https://db.sp-tarkov.com/search" target="_blank" rel="noopener noreferrer"
                      className="text-tarkov-accent hover:text-tarkov-accent-hover underline">db.sp-tarkov.com</a>
                  </p>
                </Field>
              )}
            </div>
          ))}
        </div>
      </div>

      {/* Reward Scaling */}
      <div>
        <h4 className="text-sm font-semibold text-tarkov-text-dim flex items-center gap-2 mb-3">
          <Clock size={14} /> Reward Scaling
        </h4>
        <div className="bg-tarkov-bg rounded-lg p-4">
          <div className="grid grid-cols-2 md:grid-cols-5 gap-3">
            <Field label="XP / Count" tooltip="XP per objective count. E.g. 500 × 7 kills = 3500 XP.">
              <input type="number" className="input-field text-sm" value={template.rewardScaling.xpPerObjectiveCount}
                onChange={e => onChange({ rewardScaling: { ...template.rewardScaling, xpPerObjectiveCount: Number(e.target.value) } })} min={0} />
            </Field>
            <Field label="Base Money" tooltip="Base money reward before scaling.">
              <input type="number" className="input-field text-sm" value={template.rewardScaling.baseMoney}
                onChange={e => onChange({ rewardScaling: { ...template.rewardScaling, baseMoney: Number(e.target.value) } })} min={0} />
            </Field>
            <Field label="Money / Count" tooltip="Additional money per objective count.">
              <input type="number" className="input-field text-sm" value={template.rewardScaling.moneyPerObjectiveCount}
                onChange={e => onChange({ rewardScaling: { ...template.rewardScaling, moneyPerObjectiveCount: Number(e.target.value) } })} min={0} />
            </Field>
            <Field label="Currency">
              <select className="input-field text-sm" value={template.rewardScaling.currency}
                onChange={e => onChange({ rewardScaling: { ...template.rewardScaling, currency: e.target.value } })}>
                <option value="RUB">RUB</option>
                <option value="USD">USD</option>
                <option value="EUR">EUR</option>
              </select>
            </Field>
            <Field label="Standing" tooltip="Trader standing gained per quest.">
              <input type="number" className="input-field text-sm" value={template.rewardScaling.standing}
                onChange={e => onChange({ rewardScaling: { ...template.rewardScaling, standing: Number(e.target.value) } })} step={0.01} />
            </Field>
          </div>
        </div>
      </div>

      {errors.length > 0 && (
        <div className="text-sm text-tarkov-error space-y-1">
          {errors.map((e, i) => (
            <div key={i} className="flex items-center gap-1.5">
              <AlertCircle size={12} /> {e.message}
            </div>
          ))}
        </div>
      )}
    </div>
  )
}

// Missing import from lucide
function Star({ size, className }: { size: number; className?: string }) {
  return (
    <svg xmlns="http://www.w3.org/2000/svg" width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className={className}>
      <polygon points="12 2 15.09 8.26 22 9.27 17 14.14 18.18 21.02 12 17.77 5.82 21.02 7 14.14 2 9.27 8.91 8.26 12 2" />
    </svg>
  )
}

function TooltipIcon({ text }: { text: string }) {
  const [show, setShow] = useState(false)
  return (
    <div className="relative inline-block">
      <HelpCircle
        size={12}
        className="text-tarkov-text-dim cursor-help"
        onMouseEnter={() => setShow(true)}
        onMouseLeave={() => setShow(false)}
      />
      {show && (
        <div className="absolute left-4 bottom-0 bg-tarkov-bg border border-tarkov-border rounded p-2 text-xs text-tarkov-text whitespace-nowrap z-50 shadow-lg">
          {text}
        </div>
      )}
    </div>
  )
}
