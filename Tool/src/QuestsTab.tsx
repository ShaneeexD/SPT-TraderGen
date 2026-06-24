import { useState, useCallback, useMemo } from 'react'
import {
  Plus, Trash2, ChevronDown, ChevronUp, RefreshCw, Target, Crosshair,
  Clock, MapPin, HelpCircle, AlertCircle, Upload, Image as ImageIcon,
  Scroll, Repeat, GripVertical, Copy, Package, Search, ClipboardPaste,
} from 'lucide-react'
import type {
  QuestPackDefinition, StoryQuestDefinition, QuestObjective, QuestRewards, SkillReward,
  CustomPocketDefinition, PocketSlot, RewardItem, AssortChildItem,
  RotatingQuestTemplate, RotatingObjectiveTemplate, ValidationError,
} from './types'
import { useItemNames } from './useItemNames'
import {
  createDefaultStoryQuest, createDefaultObjective, createDefaultRotatingTemplate,
  createDefaultRotatingObjective, generateMongoId, createDefaultAssortChild,
  MAP_LOCATIONS, OBJECTIVE_TYPES, ZONE_TYPES, ENEMY_TARGETS, ROTATION_TYPES, QuestZone,
} from './types'
import { ChildItemTree } from './ChildItemTree'

// Immutably update a nested child tree inside a RewardItem.
function produceRewardChildUpdate(
  item: RewardItem,
  path: number[],
  updater: (node: AssortChildItem) => AssortChildItem
): RewardItem {
  if (path.length === 0) return item
  const newChildren = [...(item.children || [])]
  let current: AssortChildItem[] = newChildren
  for (let depth = 0; depth < path.length - 1; depth++) {
    const idx = path[depth]
    const node = current[idx]
    const next = [...(node.children || [])]
    current[idx] = { ...node, children: next }
    current = next
  }
  const lastIdx = path[path.length - 1]
  current[lastIdx] = updater(current[lastIdx])
  return { ...item, children: newChildren }
}

// Extract names by map location (from SPT database allExtracts.json files).
// Shown in the Required Extract dropdown for survive/extract objectives.
const EXTRACTS_BY_LOCATION: Record<string, string[]> = {
  bigmap: ['ZB-1011', 'Crossroads', 'Old Gas Station', 'Trailer Park', 'RUAF Roadblock', 'Dorms V-Ex', 'EXFIL_ZB013', 'customs_sniper_exit', 'Custom_scav_pmc'],
  factory4: ['Cellars', 'Gate 3', 'Gate 0', 'Gate m', 'Gate_o'],
  factory4_day: ['Cellars', 'Gate 3', 'Gate 0', 'Gate m', 'Gate_o'],
  factory4_night: ['Cellars', 'Gate 3', 'Gate 0', 'Gate m', 'Gate_o'],
  Woods: ['RUAF Gate', 'ZB-016', 'ZB-014', 'UN Roadblock', 'South V-Ex', 'Outskirts', 'un-sec', 'wood_sniper_exit', 'Factory Gate'],
  Shoreline: ['Road to Customs', 'Shorl_V-Ex', 'Road_at_railbridge', 'Tunnel', 'Lighthouse_pass', 'Smugglers_Trail_coop', 'Pier Boat', 'Rock Passage'],
  Interchange: ['SE Exfil', 'NW Exfil', 'PP Exfil', 'Hole Exfill', 'Saferoom Exfil', 'Interchange Cooperation'],
  Reserve: ['EXFIL_Train', 'EXFIL_Bunker_D2', 'EXFIL_Bunker', 'Alpinist', 'EXFIL_ScavCooperation', 'EXFIL_vent', 'Exit1', 'Exit2', 'Exit3', 'Exit4'],
  Lighthouse: ['EXFIL_Train', ' V-Ex_light'],
  laboratory: ['lab_Elevator_Cargo', 'lab_Elevator_Main', 'lab_Vent', 'lab_Elevator_Med', 'lab_Under_Storage_Collector', 'lab_Parking_Gate', 'lab_Hangar_Gate'],
  TarkovStreets: ['E8_yard', 'E7_car', 'E9_sgr', 'E5_5'],
  Sandbox: ['Sandbox_VExit', 'Unity_free_exit', 'Scav_coop_exit', 'Nakatani_stairs_free_exit', 'Sniper_exit'],
}

// Known pocket template IDs for pocket upgrade reward.
const KNOWN_POCKETS: { id: string; label: string }[] = [
  { id: '557ffd194bdc2d28148b457f', label: 'Default (1x4 / 2x2)' },
  { id: '5af99e9186f7747c447120b8', label: 'Large (2x3)' },
  { id: '60c7272c204bc17802313365', label: '1x3' },
  { id: '65e080be269cbd5c5005e529', label: '+2 (Old Patterns Quest)' },
  { id: 'aabbccdd11223344556677aa', label: 'Cross (TraderGen custom)' },
]

// Valid skill names for skill reward dropdowns.
const VALID_SKILLS = [
  'Endurance', 'Strength', 'Vitality', 'Health', 'StressResistance',
  'Metabolism', 'Immunity', 'Perception', 'Intellect', 'Attention',
  'Charisma', 'Memory', 'MagDrills', 'RecoilControl', 'CovertMovement',
  'ProneMovement', 'FirstAid', 'FieldMedicine', 'Surgery', 'LightVests',
  'HeavyVests', 'WeaponModding', 'AdvancedModding', 'NightOps', 'SilentOps',
  'Lockpicking', 'Search', 'WeaponTreatment', 'Freetrading', 'Auctions',
  'Cleanoperations', 'Barter', 'Shadowconnections', 'Taskperformance',
  'Pistol', 'Revolver', 'SMG', 'Assault', 'Shotgun', 'Sniper', 'LMG',
  'DMR', 'Melee', 'Throwing', 'DrawMaster', 'AimMaster', 'Sniping',
  'TroubleShooting', 'HideoutManagement', 'Crafting',
  'BearAssaultoperations', 'BearAuthority', 'BearAksystems', 'BearHeavycaliber', 'BearRawpower',
  'UsecArsystems', 'UsecDeepweaponmodding', 'UsecLongrangeoptics', 'UsecNegotiations', 'UsecTactics',
]

// Returns true if an objective location matches (or is compatible with) the quest location.
// Only the composite 'factory4' quest location covers both day and night objectives.
function locationsMatch(objLocation: string, questLocation: string): boolean {
  if (objLocation === questLocation) return true
  if (questLocation === 'factory4' && (objLocation === 'factory4_day' || objLocation === 'factory4_night')) return true
  return false
}

// ==================== Shared sub-components ====================

function Field({ label, error, tooltip, children }: {
  label: React.ReactNode; error?: boolean; tooltip?: string; children: React.ReactNode
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

export default function QuestsTab({ questPack, traderId, onChange, onImportFromClipboard, errors }: {
  questPack: QuestPackDefinition
  traderId: string
  onChange: (pack: QuestPackDefinition) => void
  onImportFromClipboard: () => Promise<import('./types').RewardItem | undefined>
  errors: ValidationError[]
}) {
  const [activeSection, setActiveSection] = useState<'story' | 'rotating' | 'zones'>('story')
  const [expandedQuest, setExpandedQuest] = useState<number | null>(null)
  const [expandedRotating, setExpandedRotating] = useState<number | null>(null)
  const [searchQuery, setSearchQuery] = useState('')

  const filteredStoryQuests = searchQuery.trim()
    ? questPack.storyQuests.filter(q => {
        const sq = searchQuery.toLowerCase()
        return q.id.toLowerCase().includes(sq) || (q.name || '').toLowerCase().includes(sq)
      })
    : questPack.storyQuests

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
        <button
          onClick={() => setActiveSection('zones')}
          className={`flex-1 flex items-center justify-center gap-2 px-4 py-2 rounded text-sm font-medium transition-colors ${
            activeSection === 'zones' ? 'bg-tarkov-accent text-tarkov-bg' : 'text-tarkov-text-dim hover:text-tarkov-text'
          }`}
        >
          <MapPin size={16} /> Quest Zones
          {(questPack.zones?.length ?? 0) > 0 && (
            <span className={`ml-1 text-xs px-1.5 py-0.5 rounded-full ${
              activeSection === 'zones' ? 'bg-tarkov-bg/30 text-tarkov-bg' : 'bg-tarkov-accent/20 text-tarkov-accent'
            }`}>{questPack.zones.length}</span>
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

          <div className="flex items-center gap-2">
            <Search size={14} className="text-tarkov-text-dim shrink-0" />
            <input
              type="text"
              placeholder="Search by quest ID or name..."
              className="input-field text-sm flex-1"
              value={searchQuery}
              onChange={e => setSearchQuery(e.target.value)}
            />
            {searchQuery && (
              <button onClick={() => setSearchQuery('')} className="btn-secondary text-xs px-2">
                Clear
              </button>
            )}
          </div>

          {questPack.storyQuests.length === 0 && (
            <div className="card text-center text-tarkov-text-dim py-8">
              No story quests defined. Add one to get started.
            </div>
          )}

          {filteredStoryQuests.length === 0 && searchQuery && (
            <div className="card text-center text-tarkov-text-dim py-6">
              No quests match "{searchQuery}".
            </div>
          )}

          <div className="space-y-2">
            {filteredStoryQuests.map((quest, qi) => {
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
                      onImportFromClipboard={onImportFromClipboard}
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

      {/* Zones Section */}
      {activeSection === 'zones' && (
        <ZoneManager
          zones={questPack.zones ?? []}
          onChange={zones => onChange({ ...questPack, zones })}
        />
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

function StoryQuestEditor({ quest, questIndex, allQuests, onChange, onImportFromClipboard, errors }: {
  quest: StoryQuestDefinition
  questIndex: number
  allQuests: StoryQuestDefinition[]
  onChange: (updates: Partial<StoryQuestDefinition>) => void
  onImportFromClipboard: () => Promise<RewardItem | undefined>
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
        // Factory composite covers both day and night; don't warn if quest is factory4
        const isFactoryComposite = quest.location === 'factory4' && uniqueLocations.every(loc => locationsMatch(loc, quest.location))
        const needsAnyLocation = uniqueLocations.length > 1 && quest.location !== 'any' && !isFactoryComposite

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
            const locationMismatch = objLocation && questLocation !== 'any' && !locationsMatch(objLocation, questLocation)

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
          <Star size={16} /> Rewards <span className="text-tarkov-error text-[11px] font-normal">(pockets not working yet)</span>
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
            <button
              onClick={async () => {
                const imported = await onImportFromClipboard()
                if (imported) {
                  const newItems = [...(quest.rewards.items || []), imported]
                  updateRewards({ items: newItems })
                }
              }}
              className="btn-secondary text-xs flex items-center gap-1 px-2 py-1"
            >
              <ClipboardPaste size={12} /> Import from TraderGen
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
            <div className="mt-2 space-y-2">
              {(quest.rewards.items || []).map((item, idx) => (
                <div key={idx} className="bg-tarkov-bg rounded p-2 border border-tarkov-border/50">
                  <div className="flex items-start gap-2">
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

                  {/* Child attachments for this reward item */}
                  <div className="mt-2 pt-2 border-t border-tarkov-border/30">
                    <ChildItemTree
                      children={item.children || []}
                      path={[]}
                      onAdd={(path) => {
                        const newItems = [...(quest.rewards.items || [])]
                        const target = newItems[idx]
                        if (path.length === 0) {
                          newItems[idx] = { ...target, children: [...(target.children || []), createDefaultAssortChild()] }
                        } else {
                          newItems[idx] = produceRewardChildUpdate(target, path, node => ({
                            ...node,
                            children: [...(node.children || []), createDefaultAssortChild()],
                          }))
                        }
                        updateRewards({ items: newItems })
                      }}
                      onRemove={(path) => {
                        const newItems = [...(quest.rewards.items || [])]
                        const target = newItems[idx]
                        if (path.length === 1) {
                          newItems[idx] = { ...target, children: (target.children || []).filter((_, j) => j !== path[0]) }
                        } else {
                          const parentPath = path.slice(0, -1)
                          const childIdx = path[path.length - 1]
                          newItems[idx] = produceRewardChildUpdate(target, parentPath, node => ({
                            ...node,
                            children: (node.children || []).filter((_, j) => j !== childIdx),
                          }))
                        }
                        updateRewards({ items: newItems })
                      }}
                      onUpdate={(path, key, value) => {
                        const newItems = [...(quest.rewards.items || [])]
                        const target = newItems[idx]
                        if (path.length === 1) {
                          newItems[idx] = {
                            ...target,
                            children: (target.children || []).map((c, j) =>
                              j === path[0] ? { ...c, [key]: value } : c
                            ),
                          }
                        } else {
                          newItems[idx] = produceRewardChildUpdate(target, path, node => ({ ...node, [key]: value }))
                        }
                        updateRewards({ items: newItems })
                      }}
                    />
                  </div>
                </div>
              ))}
            </div>
          )}

          {/* Stash Rows */}
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4 pt-2 border-t border-tarkov-border/30">
            <Field label="Stash Rows" tooltip="Number of stash rows to add on completion. Requires client restart.">
              <input type="number" className="input-field" value={quest.rewards.stashRows || 0}
                onChange={e => updateRewards({ stashRows: Number(e.target.value) || undefined })} min={0} />
            </Field>
            <Field label="Pockets" tooltip="Pocket template to upgrade to. Requires client restart.">
              <div className="flex flex-col gap-1.5">
                <select
                  className="input-field text-sm"
                  value={quest.rewards.pockets ? (KNOWN_POCKETS.some(p => p.id === quest.rewards.pockets) ? quest.rewards.pockets : '') : (quest.rewards.customPocket ? '__custom__' : '')}
                  onChange={e => {
                    const val = e.target.value
                    if (val === '__custom__') {
                      updateRewards({ pockets: undefined, customPocket: { slots: [{ width: 1, height: 2 }] } })
                    } else if (val === '') {
                      updateRewards({ pockets: undefined, customPocket: undefined })
                    } else {
                      updateRewards({ pockets: val, customPocket: undefined })
                    }
                  }}
                >
                  <option value="">No pocket upgrade</option>
                  {KNOWN_POCKETS.map(p => (
                    <option key={p.id} value={p.id}>{p.label}</option>
                  ))}
                  <option value="__custom__">Custom...</option>
                </select>

                {/* Custom pocket editor */}
                {quest.rewards.customPocket && (
                  <CustomPocketEditor
                    definition={quest.rewards.customPocket}
                    onChange={def => updateRewards({ customPocket: def })}
                  />
                )}
              </div>
            </Field>
          </div>

          {/* Skill Rewards */}
          <div className="pt-2 border-t border-tarkov-border/30">
            <div className="flex items-center gap-3 flex-wrap mb-2">
              <button
                onClick={() => {
                  const newSkills = [...(quest.rewards.skills || []), { name: 'Endurance', points: 100 }]
                  updateRewards({ skills: newSkills })
                }}
                className="btn-secondary text-xs flex items-center gap-1 px-2 py-1"
              >
                <Plus size={12} /> Add Skill Reward
              </button>
              <span className="text-xs text-tarkov-text-dim">100 points = +1 skill level</span>
            </div>
            {(quest.rewards.skills || []).length > 0 && (
              <div className="space-y-1">
                {(quest.rewards.skills || []).map((skill, idx) => (
                  <div key={idx} className="flex items-center gap-2 bg-tarkov-bg rounded p-1.5 border border-tarkov-border/50">
                    <select
                      className="input-field text-sm flex-1"
                      value={skill.name}
                      onChange={e => {
                        const newSkills = [...(quest.rewards.skills || [])]
                        newSkills[idx] = { ...skill, name: e.target.value }
                        updateRewards({ skills: newSkills })
                      }}
                    >
                      {VALID_SKILLS.map(s => <option key={s} value={s}>{s}</option>)}
                    </select>
                    <div className="w-28">
                      <input
                        type="number"
                        min="1"
                        className="input-field text-sm w-full text-center"
                        value={skill.points}
                        onChange={e => {
                          const newSkills = [...(quest.rewards.skills || [])]
                          newSkills[idx] = { ...skill, points: Number(e.target.value) || 1 }
                          updateRewards({ skills: newSkills })
                        }}
                      />
                    </div>
                    <button
                      onClick={() => {
                        const newSkills = (quest.rewards.skills || []).filter((_, i) => i !== idx)
                        updateRewards({ skills: newSkills.length ? newSkills : undefined })
                      }}
                      className="text-tarkov-error hover:text-tarkov-error/80 p-1"
                    >
                      <Trash2 size={12} />
                    </button>
                  </div>
                ))}
              </div>
            )}
          </div>
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
  const isZone = objective.type === 'zone_visit' || objective.type === 'zone_kill' || objective.type === 'zone_place_item'
  const isZoneKill = objective.type === 'zone_kill'
  const isZonePlaceItem = objective.type === 'zone_place_item'

  return (
    <div className="px-3 pb-3 space-y-3 border-t border-tarkov-border/50">
      {isZone && (
        <div className="flex items-center gap-2 mt-3 px-3 py-2 rounded bg-tarkov-error/10 border border-tarkov-error/40 text-tarkov-error text-xs">
          <AlertCircle size={13} className="shrink-0" />
          Zone-based objectives are <strong>not available in the current release</strong>. This feature is still in development.
        </div>
      )}
      <div className="grid grid-cols-2 md:grid-cols-4 gap-3 pt-3">
        <Field label="Type" tooltip="What kind of objective this is.">
          <select className="input-field text-sm" value={objective.type}
            onChange={e => {
              const t = e.target.value
              const updates: Partial<QuestObjective> = { type: t }
              if (t === 'kill_enemy') { updates.target = 'Savage'; updates.itemTpl = undefined }
              if (t === 'handover_item' || t === 'handover_fir_item') { updates.target = undefined; updates.itemTpl = ''; updates.location = undefined }
              if (t === 'survive_location' || t === 'extract_location') { updates.location = 'bigmap'; updates.target = undefined; updates.itemTpl = undefined }
              if (t === 'zone_visit') { updates.zoneId = ''; updates.target = undefined; updates.itemTpl = undefined; updates.plantItemTpl = undefined }
              if (t === 'zone_kill') { updates.zoneId = ''; updates.target = 'Savage'; updates.itemTpl = undefined; updates.plantItemTpl = undefined }
              if (t === 'zone_place_item') { updates.zoneId = ''; updates.plantItemTpl = ''; updates.target = undefined; updates.itemTpl = undefined }
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

        {isZone && (
          <Field label="Zone ID" tooltip="The zoneId of the zone defined in the Zones tab above.">
            <input className="input-field text-sm font-mono" value={objective.zoneId || ''}
              onChange={e => onChange({ zoneId: e.target.value || undefined })}
              placeholder="e.g. my_zone_woods_01" />
          </Field>
        )}

        {isZone && (
          <Field label="Location (optional)" tooltip="Restrict to a specific map. Should match the zone's map.">
            <select className="input-field text-sm" value={objective.location || ''}
              onChange={e => onChange({ location: e.target.value || undefined })}>
              <option value="">Any Map</option>
              {MAP_LOCATIONS.filter(l => l.value !== 'any').map(l => (
                <option key={l.value} value={l.value}>{l.label}</option>
              ))}
            </select>
          </Field>
        )}

        {isZoneKill && (
          <Field label="Enemy Target" tooltip="What type of enemy to kill inside the zone.">
            <select className="input-field text-sm" value={objective.target || 'Savage'}
              onChange={e => onChange({ target: e.target.value })}>
              {ENEMY_TARGETS.map(t => <option key={t.value} value={t.value}>{t.label}</option>)}
            </select>
          </Field>
        )}

        {(isZone && !isZoneKill) && (
          <Field label="Linger Time (s)" tooltip="Seconds player must stay in zone. 0 = instant trigger.">
            <input type="number" className="input-field text-sm" min={0}
              value={objective.plantTime ?? 0}
              onChange={e => onChange({ plantTime: Number(e.target.value) || undefined })} />
          </Field>
        )}

        {isZonePlaceItem && (
          <Field label="Item Template ID" tooltip="Must be a BSG quest marker item. Only marker-type items work with PlaceBeacon — arbitrary items will crash the client.">
            <input className="input-field text-sm font-mono" value={objective.plantItemTpl || ''}
              onChange={e => onChange({ plantItemTpl: e.target.value || undefined })}
              placeholder="24-char hex" maxLength={24} />
            <p className="text-xs text-tarkov-text-dim mt-1">
              ⚠️ Only BSG marker items work. Known TPLs:<br />
              <span className="font-mono">5991b51486f77447b112d44f</span> (MS2000 Marker)<br />
              <span className="font-mono">5ac78a9b86f7741cca0bbd8d</span> (Signal Jammer)<br />
              <span className="font-mono">63a0b2eabea67a6d93009e52</span> (Radio repeater)
            </p>
          </Field>
        )}
      </div>

      <Field label="Custom Description (optional)" tooltip="Override the auto-generated objective text with your own.">
        <input className="input-field text-sm" value={objective.description || ''}
          onChange={e => onChange({ description: e.target.value || undefined })}
          placeholder="Leave blank for auto-generated text" />
      </Field>

      {/* Advanced Conditions — hidden by default to keep UI beginner-friendly */}
      <AdvancedConditions objective={objective} onChange={onChange} />
    </div>
  )
}

// ==================== Advanced Conditions Sub-Component ====================

function AdvancedConditions({ objective, onChange }: {
  objective: QuestObjective
  onChange: (updates: Partial<QuestObjective>) => void
}) {
  const [open, setOpen] = useState(false)
  const [customExtract, setCustomExtract] = useState(false)
  const isKill = objective.type === 'kill_enemy'
  const isLocation = objective.type === 'survive_location' || objective.type === 'extract_location'
  if (!isKill && !isLocation) return null

  const addToList = (field: keyof QuestObjective, value: string) => {
    const arr = [...(objective[field] as string[] || []), value]
    onChange({ [field]: arr } as Partial<QuestObjective>)
  }

  const removeFromList = (field: keyof QuestObjective, idx: number) => {
    const arr = [...(objective[field] as string[] || [])]
    arr.splice(idx, 1)
    onChange({ [field]: arr.length ? arr : undefined } as Partial<QuestObjective>)
  }

  const updateListItem = (field: keyof QuestObjective, idx: number, value: string) => {
    const arr = [...(objective[field] as string[] || [])]
    arr[idx] = value
    onChange({ [field]: arr } as Partial<QuestObjective>)
  }

  const clearNum = (v: string) => {
    const n = Number(v)
    return v === '' ? null : (Number.isFinite(n) && n >= 0 ? n : null)
  }

  const availableExtracts = objective.location ? (EXTRACTS_BY_LOCATION[objective.location] || []) : []
  const currentExtract = objective.requiredExtract || ''
  const isCustom = customExtract || (currentExtract !== '' && !availableExtracts.includes(currentExtract))

  return (
    <div className="border border-tarkov-border/40 rounded-lg">
      <button
        type="button"
        onClick={() => setOpen(!open)}
        className="w-full flex items-center justify-between px-3 py-2 text-sm text-tarkov-accent hover:bg-tarkov-border/20 transition-colors rounded-t-lg"
      >
        <span className="font-medium">Advanced Conditions</span>
        {open ? <ChevronUp size={14} /> : <ChevronDown size={14} />}
      </button>

      {open && (
        <div className="px-3 pb-3 pt-2 space-y-3 border-t border-tarkov-border/30 rounded-b-lg">

          {/* Distance — only one of min/max can be set at a time */}
          {isKill && (
            <div className="grid grid-cols-2 gap-3">
              <Field label="Min Distance (m)" tooltip="Kill must be from at least this many meters away. Cannot be used together with Max Distance.">
                <input type="number" className="input-field text-sm" min={0}
                  value={objective.minDistance ?? ''}
                  onChange={e => {
                    const val = clearNum(e.target.value)
                    onChange({ minDistance: val, ...(val != null ? { maxDistance: null } : {}) })
                  }}
                  placeholder="e.g. 40" />
              </Field>
              <Field label="Max Distance (m)" tooltip="Kill must be within this many meters. Cannot be used together with Min Distance.">
                <input type="number" className="input-field text-sm" min={0}
                  value={objective.maxDistance ?? ''}
                  onChange={e => {
                    const val = clearNum(e.target.value)
                    onChange({ maxDistance: val, ...(val != null ? { minDistance: null } : {}) })
                  }}
                  placeholder="e.g. 100" />
              </Field>
            </div>
          )}

          {/* Time of day */}
          {isKill && (
            <div className="grid grid-cols-2 gap-3">
              <Field label="Time From (hour)" tooltip="In-game hour when the kill window starts (0-23). Use with Time To for night kills.">
                <input type="number" className="input-field text-sm" min={0} max={23}
                  value={objective.timeFrom ?? ''}
                  onChange={e => onChange({ timeFrom: clearNum(e.target.value) })}
                  placeholder="e.g. 22" />
              </Field>
              <Field label="Time To (hour)" tooltip="In-game hour when the kill window ends (0-23).">
                <input type="number" className="input-field text-sm" min={0} max={23}
                  value={objective.timeTo ?? ''}
                  onChange={e => onChange({ timeTo: clearNum(e.target.value) })}
                  placeholder="e.g. 5" />
              </Field>
            </div>
          )}

          {/* Weapon templates */}
          {isKill && (
            <StringListField
              label="Weapon Template IDs"
              tooltip="24-char hex IDs of weapons that must be used for the kill. Find IDs at db.sp-tarkov.com."
              items={objective.weaponTpls || []}
              placeholder="24-char hex weapon ID"
              onAdd={v => addToList('weaponTpls', v)}
              onRemove={i => removeFromList('weaponTpls', i)}
              onChangeItem={(i, v) => updateListItem('weaponTpls', i, v)}
            />
          )}

          {/* Body parts */}
          {isKill && (
            <StringListField
              label="Body Parts"
              tooltip="Body parts that must be hit. Common values: Head, Chest, Stomach, LeftArm, RightArm, LeftLeg, RightLeg."
              items={objective.bodyPart || []}
              placeholder="e.g. Head"
              onAdd={v => addToList('bodyPart', v)}
              onRemove={i => removeFromList('bodyPart', i)}
              onChangeItem={(i, v) => updateListItem('bodyPart', i, v)}
            />
          )}

          {/* Wearing / Not Wearing */}
          {isKill && (
            <StringListField
              label="Wearing (item IDs)"
              tooltip="Item template IDs the player must be wearing when making the kill. 24-char hex IDs."
              items={objective.wearing || []}
              placeholder="24-char hex item ID"
              onAdd={v => addToList('wearing', v)}
              onRemove={i => removeFromList('wearing', i)}
              onChangeItem={(i, v) => updateListItem('wearing', i, v)}
            />
          )}

          {isKill && (
            <StringListField
              label="Not Wearing (item IDs)"
              tooltip="Item template IDs the player must NOT be wearing when making the kill. 24-char hex IDs."
              items={objective.notWearing || []}
              placeholder="24-char hex item ID"
              onAdd={v => addToList('notWearing', v)}
              onRemove={i => removeFromList('notWearing', i)}
              onChangeItem={(i, v) => updateListItem('notWearing', i, v)}
            />
          )}

          {/* One Session Only */}
          <label className="flex items-center gap-2 text-sm text-tarkov-text-dim cursor-pointer">
            <input type="checkbox" className="accent-tarkov-accent"
              checked={objective.oneSessionOnly || false}
              onChange={e => onChange({ oneSessionOnly: e.target.checked || undefined })}
            />
            <span>Must be completed in a single raid (progress resets between sessions)</span>
          </label>

          {/* Required extract */}
          {isLocation && (
            <div className="space-y-2">
              <Field label="Required Extract" tooltip="Specific extract point name. Leave blank for any extract.">
                {availableExtracts.length > 0 ? (
                  <div className="flex gap-2">
                    <select
                      className="input-field text-sm flex-1"
                      value={isCustom ? '__other__' : (objective.requiredExtract || '')}
                      onChange={e => {
                        const val = e.target.value
                        if (val === '__other__') {
                          setCustomExtract(true)
                        } else if (val === '') {
                          setCustomExtract(false)
                          onChange({ requiredExtract: undefined })
                        } else {
                          setCustomExtract(false)
                          onChange({ requiredExtract: val })
                        }
                      }}
                    >
                      <option value="">Any Extract</option>
                      {availableExtracts.map(name => (
                        <option key={name} value={name}>{name}</option>
                      ))}
                      <option value="__other__">Other (custom)...</option>
                    </select>
                  </div>
                ) : (
                  <input className="input-field text-sm" value={objective.requiredExtract || ''}
                    onChange={e => onChange({ requiredExtract: e.target.value || undefined })}
                    placeholder="e.g. Factory gate 0" />
                )}
              </Field>
              {isCustom && (
                <input
                  className="input-field text-sm"
                  value={objective.requiredExtract || ''}
                  onChange={e => onChange({ requiredExtract: e.target.value || undefined })}
                  placeholder="Enter custom extract name"
                />
              )}
            </div>
          )}
        </div>
      )}
    </div>
  )
}

// Helper for array-of-strings fields with add/remove
function StringListField({ label, tooltip, items, placeholder, onAdd, onRemove, onChangeItem }: {
  label: string
  tooltip?: string
  items: string[]
  placeholder: string
  onAdd: (value: string) => void
  onRemove: (index: number) => void
  onChangeItem: (index: number, value: string) => void
}) {
  const [draft, setDraft] = useState('')
  const canAdd = draft.trim().length > 0

  return (
    <div className="space-y-2">
      <div className="flex items-center gap-1.5">
        <span className="label text-sm">{label}</span>
        {tooltip && (
          <span className="relative group">
            <HelpCircle size={13} className="text-tarkov-text-dim hover:text-tarkov-accent cursor-help transition-colors" />
            <span className="absolute bottom-full left-1/2 -translate-x-1/2 mb-2 px-3 py-2 bg-tarkov-bg border border-tarkov-border rounded-lg text-xs text-tarkov-text font-normal w-64 opacity-0 invisible group-hover:opacity-100 group-hover:visible transition-all duration-150 z-50 shadow-xl leading-relaxed pointer-events-none">
              {tooltip}
            </span>
          </span>
        )}
      </div>
      {items.length > 0 && (
        <div className="space-y-1.5">
          {items.map((item, i) => (
            <div key={i} className="flex items-center gap-2">
              <input className="input-field text-sm flex-1 font-mono" value={item}
                onChange={e => onChangeItem(i, e.target.value)} placeholder={placeholder} />
              <button onClick={() => onRemove(i)}
                className="text-tarkov-error hover:text-tarkov-error/80 p-1">
                <Trash2 size={14} />
              </button>
            </div>
          ))}
        </div>
      )}
      <div className="flex items-center gap-2">
        <input className="input-field text-sm flex-1 font-mono" value={draft}
          onChange={e => setDraft(e.target.value)}
          onKeyDown={e => { if (e.key === 'Enter' && canAdd) { onAdd(draft.trim()); setDraft('') } }}
          placeholder={placeholder} />
        <button onClick={() => { if (canAdd) { onAdd(draft.trim()); setDraft('') } }}
          className="btn-secondary text-sm flex items-center gap-1 px-2 py-1"
          disabled={!canAdd}>
          <Plus size={14} /> Add
        </button>
      </div>
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

// Pocket slot data for visual previews.
interface PocketSlotView { w: number; h: number; color: string }

const POCKET_LAYOUTS: Record<string, PocketSlotView[]> = {
  '557ffd194bdc2d28148b457f': [
    { w: 1, h: 2, color: '#4ade80' },
    { w: 1, h: 2, color: '#4ade80' },
    { w: 1, h: 2, color: '#4ade80' },
    { w: 1, h: 2, color: '#4ade80' },
  ],
  '5af99e9186f7747c447120b8': [
    { w: 1, h: 2, color: '#60a5fa' },
    { w: 1, h: 2, color: '#60a5fa' },
    { w: 1, h: 2, color: '#60a5fa' },
    { w: 1, h: 2, color: '#60a5fa' },
    { w: 1, h: 2, color: '#60a5fa' },
  ],
  '60c7272c204bc17802313365': [
    { w: 1, h: 3, color: '#f472b6' },
    { w: 1, h: 3, color: '#f472b6' },
    { w: 1, h: 3, color: '#f472b6' },
    { w: 1, h: 3, color: '#f472b6' },
  ],
  '65e080be269cbd5c5005e529': [
    { w: 1, h: 2, color: '#a78bfa' },
    { w: 1, h: 2, color: '#a78bfa' },
    { w: 1, h: 2, color: '#a78bfa' },
    { w: 1, h: 2, color: '#a78bfa' },
    { w: 1, h: 2, color: '#a78bfa' },
    { w: 1, h: 2, color: '#a78bfa' },
  ],
  'aabbccdd11223344556677aa': [
    { w: 1, h: 2, color: '#fbbf24' },
    { w: 2, h: 1, color: '#fbbf24' },
    { w: 2, h: 2, color: '#f97316' },
    { w: 2, h: 1, color: '#fbbf24' },
    { w: 1, h: 2, color: '#fbbf24' },
  ],
}

function PocketSlotBlock({ slot }: { slot: PocketSlotView }) {
  return (
    <div
      className="rounded border border-white/20 flex items-center justify-center text-[10px] font-mono text-white/90 shadow-sm"
      style={{
        width: `${slot.w * 28}px`,
        height: `${slot.h * 28}px`,
        backgroundColor: slot.color,
      }}
    >
      {slot.w}×{slot.h}
    </div>
  )
}

function PocketPreview({ pocketId }: { pocketId?: string }) {
  if (!pocketId) return null

  const layout = POCKET_LAYOUTS[pocketId]
  if (!layout) {
    return (
      <div className="mt-2 text-xs text-tarkov-text-dim italic">
        Custom pocket — preview unavailable
      </div>
    )
  }

  const totalCells = layout.reduce((sum, s) => sum + s.w * s.h, 0)

  // Cross layout gets a special visual arrangement
  if (pocketId === 'aabbccdd11223344556677aa') {
    const [top, left, center, right, bottom] = layout
    return (
      <div className="mt-2">
        <div className="text-xs text-tarkov-text-dim mb-1.5">
          {layout.length} slots · {totalCells} cells total
        </div>
        <div className="flex flex-col items-center gap-1">
          <PocketSlotBlock slot={top} />
          <div className="flex items-center gap-1">
            <PocketSlotBlock slot={left} />
            <PocketSlotBlock slot={center} />
            <PocketSlotBlock slot={right} />
          </div>
          <PocketSlotBlock slot={bottom} />
        </div>
      </div>
    )
  }

  // Default layout: slots shown in a row
  return (
    <div className="mt-2">
      <div className="text-xs text-tarkov-text-dim mb-1.5">
        {layout.length} slot(s) · {totalCells} cells total
      </div>
      <div className="flex flex-wrap gap-1">
        {layout.map((slot, i) => (
          <PocketSlotBlock key={i} slot={slot} />
        ))}
      </div>
    </div>
  )
}

function CustomPocketEditor({ definition, onChange }: {
  definition: CustomPocketDefinition
  onChange: (def: CustomPocketDefinition) => void
}) {
  const totalCells = definition.slots.reduce((sum, s) => sum + s.width * s.height, 0)

  const updateSlot = (idx: number, updates: Partial<PocketSlot>) => {
    const slots = definition.slots.map((s, i) => i === idx ? { ...s, ...updates } : s)
    onChange({ ...definition, slots })
  }

  const removeSlot = (idx: number) => {
    const slots = definition.slots.filter((_, i) => i !== idx)
    if (slots.length === 0) return
    onChange({ ...definition, slots })
  }

  const addSlot = () => {
    onChange({ ...definition, slots: [...definition.slots, { width: 1, height: 2 }] })
  }

  return (
    <div className="mt-2 space-y-2">
      <div className="text-xs text-tarkov-text-dim">
        {definition.slots.length} slot(s) · {totalCells} cells total
      </div>

      {/* Slot list */}
      <div className="space-y-1.5">
        {definition.slots.map((slot, idx) => (
          <div key={idx} className="flex items-center gap-2">
            <span className="text-xs text-tarkov-text-dim w-12">Slot {idx + 1}</span>
            <div className="flex items-center gap-1">
              <input
                type="number"
                min={1}
                max={4}
                className="input-field text-xs w-14 text-center py-1"
                value={slot.width}
                onChange={e => updateSlot(idx, { width: Math.max(1, Math.min(4, Number(e.target.value) || 1)) })}
              />
              <span className="text-xs text-tarkov-text-dim">×</span>
              <input
                type="number"
                min={1}
                max={4}
                className="input-field text-xs w-14 text-center py-1"
                value={slot.height}
                onChange={e => updateSlot(idx, { height: Math.max(1, Math.min(4, Number(e.target.value) || 1)) })}
              />
            </div>
            <button
              onClick={() => removeSlot(idx)}
              className="text-tarkov-error hover:text-tarkov-error/80 p-1"
              title="Remove slot"
            >
              <Trash2 size={12} />
            </button>
          </div>
        ))}
      </div>

      <button
        onClick={addSlot}
        className="btn-secondary text-xs flex items-center gap-1 px-2 py-1"
      >
        <Plus size={12} /> Add Slot
      </button>

      {/* Visual preview */}
      <div className="flex flex-wrap gap-1 pt-1">
        {definition.slots.map((slot, i) => (
          <div
            key={i}
            className="rounded border border-white/20 flex items-center justify-center text-[10px] font-mono text-white/90 shadow-sm bg-tarkov-accent/80"
            style={{
              width: `${slot.width * 28}px`,
              height: `${slot.height * 28}px`,
            }}
          >
            {slot.width}×{slot.height}
          </div>
        ))}
      </div>
    </div>
  )
}

// ==================== Zone Manager ====================

function createDefaultZone(): QuestZone {
  return {
    zoneId: '',
    zoneName: '',
    zoneLocation: '',
    zoneType: 'visit',
    flareType: '',
    position: { x: '0', y: '0', z: '0' },
    rotation: { x: '0', y: '0', z: '0', w: '1' },
    scale: { x: '2', y: '2', z: '2' },
  }
}

function ZoneManager({ zones, onChange }: {
  zones: QuestZone[]
  onChange: (zones: QuestZone[]) => void
}) {
  const [expanded, setExpanded] = useState<number | null>(null)

  const addZone = () => {
    const z = createDefaultZone()
    onChange([...zones, z])
    setExpanded(zones.length)
  }

  const removeZone = (i: number) => {
    onChange(zones.filter((_, idx) => idx !== i))
    if (expanded === i) setExpanded(null)
  }

  const updateZone = (i: number, updates: Partial<QuestZone>) => {
    onChange(zones.map((z, idx) => idx === i ? { ...z, ...updates } : z))
  }

  const updateVec = (i: number, field: 'position' | 'rotation' | 'scale', axis: string, val: string) => {
    const zone = zones[i]
    onChange(zones.map((z, idx) => idx === i ? { ...z, [field]: { ...zone[field], [axis]: val } } : z))
  }

  return (
    <div className="space-y-3">
      <div className="flex items-center justify-between">
        <p className="text-sm text-tarkov-text-dim">
          Define physical trigger zones on the map. Use F12 in-game (WTT-ClientCommonLib) to position them visually.
        </p>
        <button onClick={addZone} className="btn-primary text-sm flex items-center gap-1.5">
          <Plus size={14} /> Add Zone
        </button>
      </div>

      {zones.length === 0 && (
        <div className="bg-tarkov-surface border border-tarkov-border rounded-lg px-4 py-3 text-sm text-tarkov-text-dim flex items-center gap-2">
          <HelpCircle size={14} className="text-tarkov-accent shrink-0" />
          No zones defined. Zones are required for zone_visit, zone_kill, and zone_place_item objectives. Use the in-game F12 editor to place zones, then copy the coordinates here.
        </div>
      )}

      {zones.map((zone, i) => {
        const isExp = expanded === i
        const hasId = zone.zoneId.trim().length > 0
        return (
          <div key={i} className="bg-tarkov-surface border border-tarkov-border rounded-lg overflow-hidden">
            <div
              className="flex items-center gap-3 px-4 py-3 cursor-pointer hover:bg-tarkov-bg/30 transition-colors"
              onClick={() => setExpanded(isExp ? null : i)}
            >
              <MapPin size={14} className="text-tarkov-accent shrink-0" />
              <div className="flex-1 min-w-0">
                <span className="text-sm font-medium font-mono text-tarkov-text truncate">
                  {hasId ? zone.zoneId : <span className="text-tarkov-text-dim italic">Unnamed Zone</span>}
                </span>
                {zone.zoneLocation && (
                  <span className="ml-2 text-xs text-tarkov-text-dim">{zone.zoneLocation} · {zone.zoneType}</span>
                )}
              </div>
              <button onClick={e => { e.stopPropagation(); removeZone(i) }}
                className="text-tarkov-error hover:text-tarkov-error/80 shrink-0">
                <Trash2 size={14} />
              </button>
              {isExp ? <ChevronUp size={14} className="text-tarkov-text-dim" /> : <ChevronDown size={14} className="text-tarkov-text-dim" />}
            </div>

            {isExp && (
              <div className="px-4 pb-4 space-y-4 border-t border-tarkov-border/50 pt-3">
                <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
                  <Field label="Zone ID" tooltip="Unique identifier used in quest objectives' zoneId field.">
                    <input className="input-field text-sm font-mono" value={zone.zoneId}
                      onChange={e => updateZone(i, { zoneId: e.target.value })}
                      placeholder="my_zone_woods_01" />
                  </Field>
                  <Field label="Zone Name" tooltip="Display name (can match Zone ID).">
                    <input className="input-field text-sm" value={zone.zoneName}
                      onChange={e => updateZone(i, { zoneName: e.target.value })}
                      placeholder="e.g. Checkpoint Alfa" />
                  </Field>
                  <Field label="Map Location" tooltip="The SPT location ID for the map this zone is on.">
                    <select className="input-field text-sm" value={zone.zoneLocation}
                      onChange={e => updateZone(i, { zoneLocation: e.target.value })}>
                      <option value="">-- Select Map --</option>
                      {MAP_LOCATIONS.filter(l => l.value !== 'any').map(l => (
                        <option key={l.value} value={l.value}>{l.label}</option>
                      ))}
                    </select>
                  </Field>
                  <Field label="Zone Type" tooltip="How this zone is used in-game.">
                    <select className="input-field text-sm" value={zone.zoneType}
                      onChange={e => updateZone(i, { zoneType: e.target.value })}>
                      {ZONE_TYPES.map(t => <option key={t.value} value={t.value}>{t.label}</option>)}
                    </select>
                  </Field>
                </div>

                <div className="space-y-2">
                  <p className="text-xs font-semibold text-tarkov-text-dim uppercase tracking-wide">Position (X / Y / Z)</p>
                  <div className="grid grid-cols-3 gap-2">
                    {(['x', 'y', 'z'] as const).map(axis => (
                      <div key={axis}>
                        <label className="label text-[11px]">{axis.toUpperCase()}</label>
                        <input className="input-field text-sm font-mono" value={zone.position[axis]}
                          onChange={e => updateVec(i, 'position', axis, e.target.value)} placeholder="0" />
                      </div>
                    ))}
                  </div>
                </div>

                <div className="space-y-2">
                  <p className="text-xs font-semibold text-tarkov-text-dim uppercase tracking-wide">Scale (X / Y / Z)</p>
                  <div className="grid grid-cols-3 gap-2">
                    {(['x', 'y', 'z'] as const).map(axis => (
                      <div key={axis}>
                        <label className="label text-[11px]">{axis.toUpperCase()}</label>
                        <input className="input-field text-sm font-mono" value={zone.scale[axis]}
                          onChange={e => updateVec(i, 'scale', axis, e.target.value)} placeholder="2" />
                      </div>
                    ))}
                  </div>
                </div>

                <div className="space-y-2">
                  <p className="text-xs font-semibold text-tarkov-text-dim uppercase tracking-wide">Rotation (X / Y / Z / W)</p>
                  <div className="grid grid-cols-4 gap-2">
                    {(['x', 'y', 'z', 'w'] as const).map(axis => (
                      <div key={axis}>
                        <label className="label text-[11px]">{axis.toUpperCase()}</label>
                        <input className="input-field text-sm font-mono" value={zone.rotation[axis]}
                          onChange={e => updateVec(i, 'rotation', axis, e.target.value)} placeholder={axis === 'w' ? '1' : '0'} />
                      </div>
                    ))}
                  </div>
                </div>

                <div className="bg-tarkov-bg/40 border border-tarkov-border/50 rounded px-3 py-2 text-xs text-tarkov-text-dim flex items-start gap-2">
                  <HelpCircle size={12} className="text-tarkov-accent shrink-0 mt-0.5" />
                  Tip: Use the <strong>F12 in-game editor</strong> (requires WTT-ClientCommonLib) to visually place and resize zones, then copy the generated coordinates here.
                </div>
              </div>
            )}
          </div>
        )
      })}
    </div>
  )
}
