using System;
using Server;
using Server.Spells;
using Server.Network;
using Server.Mobiles;
using System.Collections;
using System.Collections.Generic;
using Server.Engines.PartySystem;
using Server.Targeting;
using Server.Items;
using Server.Commands;
using System.Linq;

namespace Server.Spells.SkillMasteries
{
	public abstract class SkillMasterySpell : Spell
	{
        public static void Initialize()
        {
            CommandSystem.Register("LearnAllMasteries", AccessLevel.GameMaster, LearnAllSpells);
        }

        public UpkeepTimer Timer { get; set; }
        public Mobile Target { get; set; }
        public List<Mobile> PartyList { get; set; }
        public DateTime Expires { get; set; }

        public virtual double RequiredSkill { get { return 90.0; } }
        public virtual double UpKeep { get { return 0; } }
        public virtual int RequiredMana { get { return 10; } }
        public virtual bool PartyEffects { get { return false; } }
        public virtual int DamageThreshold { get { return 45; } }
        public virtual bool DamageCanDisrupt { get { return false; } }
        public virtual int TickTime { get { return 2; } }
        public virtual int PartyRange { get { return 12; } }

        public virtual int UpkeepCancelMessage { get { return 1156111; } } // You do not have enough mana to keep your ability active.
        public virtual int OutOfRangeMessage { get { return 1156098; } } // Your target is no longer in range of your ability.
        public virtual int DisruptMessage { get { return 1156110; } } // Your ability was canceled. 

        public virtual bool CancelsWeaponAbility { get { return false; } }
        public virtual bool CancelsSpecialMove { get { return CancelsWeaponAbility; } }

        public virtual TimeSpan ExpirationPeriod { get { return TimeSpan.FromMinutes(30); } }
        public override TimeSpan CastDelayBase { get { return TimeSpan.FromSeconds(2.0); } }

		public virtual double BaseSkillBonus 
		{
			get
			{
				if(Caster == null)
					return 0.0;
					
				return (Caster.Skills[CastSkill].Value + Caster.Skills[DamageSkill].Value + (GetMasteryLevel() * 40)) / 3;
			}
		}

        public virtual double CollectiveBonus
        {
            get
            {
                return 0;
            }
        }
		
		public override bool ClearHandsOnCast{ get{ return false; } }
		public override bool BlocksMovement{ get{ return true; } }

		public SkillMasterySpell( Mobile caster, Item scroll, SpellInfo info ) : base( caster, null, info )
		{
		}
		
		public override bool CheckCast()
		{
			int mana = ScaleMana( RequiredMana );

			if ( !base.CheckCast() )
				return false;

            if (IsInCooldown(Caster, this.GetType()))
                return false;

            if (Caster.Skills[CastSkill].Value < RequiredSkill)
                Caster.SendLocalizedMessage(1115709); // Your skills are not high enough to invoke this mastery ability.
            else if (Caster is PlayerMobile && Caster.Skills.CurrentMastery != CastSkill)
                Caster.SendLocalizedMessage(1115664); // You are not on the correct path for using this mastery ability.
            else if (Caster is PlayerMobile && !MasteryInfo.HasLearned(Caster, this.GetType(), CastSkill))
                Caster.SendMessage("You have not proficient in that mastery to use."); // Todo: MEssage?
            else if (Caster.Mana < mana)
                Caster.SendLocalizedMessage(1060174, mana.ToString()); // You must have at least ~1_MANA_REQUIREMENT~ Mana to use this ability.
            else
            {
                if (CancelsWeaponAbility)
                    WeaponAbility.ClearCurrentAbility(Caster);

                if (CancelsSpecialMove)
                    SpecialMove.ClearCurrentMove(Caster);

                return true;
            }

			return false;
		}

        public override void DoFizzle()
        {
            Caster.LocalOverheadMessage(MessageType.Regular, 0x3B2, 502632); // The spell fizzles.

            if (Caster.Player)
            {
                Caster.FixedParticles(0x3735, 1, 30, 9503, EffectLayer.Waist);

                //m_Caster.PlaySound(0x5C);
            }
        }

        public virtual void SendCastEffect()
        {
            Caster.FixedEffect(0x37C4, 10, (int)(GetCastDelay().TotalSeconds * 28), 4, 3);
        }

		public override void OnBeginCast()
		{
			base.OnBeginCast();

			SendCastEffect();
		}

		public override void GetCastSkills( out double min, out double max )
		{
            min = RequiredSkill;
			max = RequiredSkill + 25.0;
		}
		
		public override int GetMana()
		{
			return RequiredMana;
		}

        public BaseWeapon GetWeapon()
        {
            return Caster.Weapon as BaseWeapon;
        }

        public bool CheckWeapon()
        {
            BaseWeapon wep = GetWeapon();

            if (CastSkill == SkillName.Poisoning && wep != null && !(wep is Fists))
                return true;

            return wep != null && wep.DefSkill == CastSkill;
        }

		public virtual bool OnTick()
		{
            Caster.RevealingAction();
            int upkeep = ScaleUpkeep();

            if ((Caster is PlayerMobile && Caster.NetState == null) || Expires < DateTime.UtcNow)
                Expire();
            else if (Target != null && !Target.Alive)
                Expire();
            else if (Target != null && !Caster.InRange(Target.Location, PartyRange))
            {
                Expire();

                if(OutOfRangeMessage > 0)
                    Caster.SendLocalizedMessage(OutOfRangeMessage);
            }
            else if (Caster.Mana < upkeep)
            {
                if(UpkeepCancelMessage > 0)
                    Caster.SendLocalizedMessage(UpkeepCancelMessage);

                Expire();
            }
            else
            {
                DoEffects();
                Caster.Mana -= upkeep;
                return true;
            }

            return false;
		}

        protected virtual void DoEffects()
        {
        }

		public virtual int PropertyBonus()
		{
			return 0;
		}
		
		public virtual int PropertyBonus2()
		{
			return 0;
		}
		
		//Used for hits/stam/mana
		public virtual int StatBonus()
		{
			return 0;
		}
		
		public virtual void AddStatMods()
		{
		}
		
		public virtual void RemoveStatMods()
		{
		}

        public virtual void EndEffects()
        {
        }
		
		public virtual int DamageBonus()
		{
			return 0;
		}
		
		public virtual void AbsorbDamage(ref int damage)
		{
		}
		
		public virtual void DoDamage(Mobile victim, int damageTaken)
		{
		}

        public virtual void OnHit(Mobile defender, ref int damage)
		{
        }
		
		public virtual void OnGotHit(Mobile attacker, ref int damage)
		{
		}

        public virtual void OnMiss(Mobile defender)
        {
        }

        public virtual void OnGotMiss(Mobile attacker)
        {
        }

        public virtual void OnParried(Mobile attacker)
        {
        }

        public virtual void OnGotParried(Mobile defender)
        {
        }

        public virtual void OnDamaged(Mobile attacker, int damage)
        {
        }

        public virtual void OnWeaponRemoved(BaseWeapon weapon)
        {
        }
		
		public virtual int ScaleUpkeep()
		{
            if (UpKeep == 0)
                return 0;

            double mod = CollectiveBonus;

            double upkeep = UpKeep;
            int mana = (int)(upkeep - ((upkeep * mod) / 4.5));
			
			return ScaleMana(mana);
		}
		
		public virtual void Expire(bool disrupt = false)
		{
            if (Timer != null)
            {
                Timer.Stop();
                Timer = null;
            }
				
			if(disrupt && DisruptMessage > 0)
				Caster.SendLocalizedMessage(DisruptMessage);
				
			RemoveFromTable();
			RemoveStatMods();
            EndEffects();

            Caster.Delta(MobileDelta.WeaponDamage);

            if(Target != null)
                Target.Delta(MobileDelta.WeaponDamage);

            if (PartyList != null)
            {
                foreach(Mobile m in PartyList)
                    m.Delta(MobileDelta.WeaponDamage);

                PartyList.Clear();
                PartyList.TrimExcess();
            }

            OnExpire();
		}

        public virtual void OnExpire()
        {
        }
		
		public virtual double DamageModifier(Mobile victim)
		{
            double dSkill = Caster.Skills[DamageSkill].Value;
			double vSkill = victim.Skills[SkillName.MagicResist].Value;
				
			double reduce = (dSkill - vSkill) / dSkill;
				
			if(reduce < 0) reduce = 0;	
			if(reduce > 1) reduce = 1;
			
			return reduce;
		}

        public virtual bool CheckResisted(Mobile target)
        {
            int volumeMod = GetMasteryLevel();
            volumeMod *= 2;

            double n = GetResistPercent(target, volumeMod);

            n /= 100.0;

            if (n <= 0.0)
                return false;

            if (n >= 1.0)
                return true;

            int maxSkill = (1 + volumeMod) * 10;
            maxSkill += (1 + (volumeMod / 6)) * 25;

            if (target.Skills[SkillName.MagicResist].Value < maxSkill)
                target.CheckSkill(SkillName.MagicResist, 0.0, target.Skills[SkillName.MagicResist].Cap);

            return (n >= Utility.RandomDouble());
        }

        public virtual double GetResistPercent(Mobile target, int level)
        {
            double firstPercent = target.Skills[SkillName.MagicResist].Value / 5.0;
            double secondPercent = target.Skills[SkillName.MagicResist].Value - (((Caster.Skills[CastSkill].Value - 20.0) / 5.0) + (1 + level) * 5.0);

            return (firstPercent > secondPercent ? firstPercent : secondPercent) / 2.0;
        }

        public int GetMasteryLevel()
        {
            return (int)MasteryInfo.GetMasteryLevel(Caster, CastSkill);
        }

		
		public List<Mobile> GetParty()
		{
			if(!PartyEffects)
				return null;
				
			Party p = Party.Get( Caster );
            List<Mobile> list = new List<Mobile>();

            if (p != null)
            {
                IPooledEnumerable eable = Caster.Map.GetMobilesInRange(Caster.Location, PartyRange);

                foreach (Mobile mob in eable)
                {
                    Mobile check = mob;

                    if (mob is BaseCreature && (((BaseCreature)mob).Summoned || ((BaseCreature)mob).Controlled))
                        check = ((BaseCreature)mob).GetMaster();

                    if (check != null && p.Contains(check))
                    {
                        list.Add(mob);

                        if (PartyList == null)
                            PartyList = new List<Mobile>();

                        if (!PartyList.Contains(mob))
                            PartyList.Add(mob);
                    }
                }

                eable.Free();
            }

            if (!list.Contains(Caster))
                list.Add(Caster);

			return list;
		}
		
		private static Dictionary<Mobile, List<SkillMasterySpell>> m_Table = new Dictionary<Mobile, List<SkillMasterySpell>>();

        public static SkillMasterySpell GetHarmfulSpell(Mobile target, Type type)
        {
            foreach (Mobile m in m_Table.Keys)
            {
                foreach (SkillMasterySpell spell in m_Table[m])
                {
                    if (spell != null && spell.GetType() == type && spell.Target == target)
                        return spell;
                }
            }

            return null;
        }
		
		public static bool HasHarmfulEffects(Mobile target, Type type)
		{
            foreach (Mobile m in m_Table.Keys)
            {
                foreach (SkillMasterySpell spell in m_Table[m])
                {
                    if (spell != null && spell.GetType() == type && spell.Target == target)
                        return true;
                }
            }
			
			return false;
		}
		
		public static SkillMasterySpell GetSpell(Mobile from, Type type)
		{
			if(m_Table.ContainsKey(from))
			{
				if(m_Table[from] == null || m_Table[from].Count == 0)
					m_Table.Remove(from);
				else
				{
                    foreach (SkillMasterySpell spell in m_Table[from])
                    {
                        if (spell != null && spell.GetType() == type)
                            return spell;
                    }
				}
			}
			
			return null;
		}

        public static SkillMasterySpell GetSpell(Func<SkillMasterySpell, bool> predicate)
		{
            foreach (SkillMasterySpell spell in EnumerateAllSpells())
            {
                if (predicate != null && predicate(spell))
                    return spell;
            }
           
			return null;
		}

        public static IEnumerable<SkillMasterySpell> GetSpells(Func<SkillMasterySpell, bool> predicate)
        {
            foreach (SkillMasterySpell spell in EnumerateAllSpells())
            {
                if (spell != null && predicate != null && predicate(spell))
                {
                    yield return spell;
                }
            }
        }

        public static IEnumerable<SkillMasterySpell> GetSpells(Mobile caster, Func<SkillMasterySpell, bool> predicate)
        {
            foreach (SkillMasterySpell spell in EnumerateSpells(caster))
            {
                if (spell != null && predicate != null && predicate(spell))
                {
                    yield return spell;
                }
            }
        }

        public static bool HasSpell(Mobile from, Type type)
        {
            if (m_Table.ContainsKey(from))
            {
                if (m_Table[from] == null || m_Table[from].Count == 0)
                    m_Table.Remove(from);
                else
                {
                    foreach (SkillMasterySpell spell in m_Table[from])
                    {
                        if (spell != null && spell.GetType() == type)
                            return true;
                    }
                }
            }

            return false;
        }

        public static SkillMasterySpell GetSpellForParty(Mobile from, Type type)
        {
            //First checks the bard
            if (m_Table.ContainsKey(from))
            {
                if (m_Table[from] == null || m_Table[from].Count == 0)
                {
                    m_Table.Remove(from);
                    return null;
                }

                foreach (SkillMasterySpell spell in m_Table[from])
                    if (spell != null && spell.GetType() == type)
                        return spell;
            }
            else
            {
                Mobile check = from;

                if (from is BaseCreature && (((BaseCreature)from).Controlled || ((BaseCreature)from).Summoned))
                    check = ((BaseCreature)from).GetMaster();

                Party p = Party.Get(check);

                if (p != null)
                {
                    foreach (PartyMemberInfo info in p.Members)
                    {
                        SkillMasterySpell spell = GetSpell(info.Mobile, type);

                        if (spell != null && spell.PartyEffects && from.InRange(info.Mobile.Location, spell.PartyRange))
                            return spell;
                    }
                }
            }

            return null;
        }

        private static object _AllSpellsLock = new object();

        public static IEnumerable<SkillMasterySpell> EnumerateAllSpells()
        {
            if (m_Table == null || m_Table.Count == 0)
                yield break;

            List<SkillMasterySpell> list = new List<SkillMasterySpell>();

            lock (_AllSpellsLock)
            {
                foreach (KeyValuePair<Mobile, List<SkillMasterySpell>> kvp in m_Table)
                {
                    list.AddRange(kvp.Value);
                }
            }

            foreach (SkillMasterySpell spell in list)
            {
                yield return spell;
            }
        }

        private static object _Lock = new object();

        public static IEnumerable<SkillMasterySpell> EnumerateSpells(Mobile from, Type t = null)
        {
            if (m_Table == null || m_Table.Count == 0 || !m_Table.ContainsKey(from))
                yield break;

            List<SkillMasterySpell> list;

            lock (_Lock)
            {
                list = m_Table[from];

                if (list == null || list.Count == 0)
                {
                    yield break;
                }
            }

            IEnumerable<SkillMasterySpell> e;

            lock (_Lock)
            {
                e = list.AsParallel().Where(s => s.GetType() == t || t == null);
            }

            foreach (SkillMasterySpell spell in e)
            {
                yield return spell;
            }
        }

        public static List<SkillMasterySpell> GetSpells(Mobile m)
        {
            if (m_Table != null && m_Table.ContainsKey(m))
            {
                return new List<SkillMasterySpell>(m_Table[m]);
            }

            return null;
        }
		
		protected void AddToTable(Mobile from, SkillMasterySpell spell)
		{
			if(!m_Table.ContainsKey(from))
				m_Table[from] = new List<SkillMasterySpell>();
				
			m_Table[from].Add(spell);
		}
		
		protected void RemoveFromTable()
		{
			if(m_Table.ContainsKey(Caster) && m_Table[Caster].Contains(this))
			{
				m_Table[Caster].Remove(this);
				
				if(m_Table[Caster].Count == 0)
					m_Table.Remove(Caster);
			}
		}
		
        /// <summary>
        /// Called in Aos.cs, should include all damage types
        /// </summary>
        /// <param name="victim"></param>
        /// <param name="damager"></param>
        /// <param name="damage"></param>
		public static void OnCasterDamaged(Mobile victim, Mobile damager, ref int damage)
		{
			if(victim == null)
				return;

            if (m_Table.ContainsKey(victim))
            {
                if (m_Table[victim] == null || m_Table[victim].Count == 0)
                    m_Table.Remove(victim);
                else
                {
                    foreach (SkillMasterySpell sp in EnumerateSpells(victim))
                    {
                        if (sp.DamageCanDisrupt && damage > sp.DamageThreshold)
                            sp.Expire(true);

                        sp.OnDamaged(damager, damage);
                    }
                }
            }

            SkillMasteryMove move = SpecialMove.GetCurrentMove(victim) as SkillMasteryMove;

            if (move != null)
                move.OnDamaged(damager, victim, damage);

            //CombatTrainingSpell.CheckDamage(damager, victim, ref damage);
		}

        /// <summary>
        /// Called in BaseWeapon, intended as a melee/ranged hit
        /// </summary>
        /// <param name="attacker"></param>
        /// <param name="defender"></param>
        /// <param name="damage"></param>
        public static void OnHit(Mobile attacker, Mobile defender, ref int damage)
		{
			foreach(SkillMasterySpell spell in EnumerateSpells(attacker))
			{
				spell.OnHit(defender, ref damage);
			}

            foreach (SkillMasterySpell spell in EnumerateSpells(defender))
            {
                spell.OnGotHit(defender, ref damage);
            }

            SkillMasteryMove move = SpecialMove.GetCurrentMove(defender) as SkillMasteryMove;

            if (move != null)
                move.OnGotHit(attacker, defender, ref damage);

            /*if(attacker is BaseCreature || defender is BaseCreature)
                CombatTrainingSpell.OnCreatureHit(attacker, defender, ref damage);*/
        }

        /// <summary>
        /// Called in BaseWeapon, intended as a melee/ranged miss
        /// </summary>
        /// <param name="attacker"></param>
        /// <param name="defender"></param>
        public static void OnMiss(Mobile attacker, Mobile defender)
        {
            foreach (SkillMasterySpell spell in EnumerateSpells(attacker))
            {
                spell.OnMiss(defender);
            }

            foreach (SkillMasterySpell spell in EnumerateSpells(defender))
            {
                spell.OnGotMiss(attacker);
            }
        }

        /// <summary>
        /// Called in BaseWeapon, intended as a melee/ranged parry
        /// </summary>
        /// <param name="attacker"></param>
        /// <param name="defender"></param>
        public static void OnParried(Mobile attacker, Mobile defender)
        {
            foreach (SkillMasterySpell spell in EnumerateSpells(defender))
            {
                spell.OnParried(attacker);
            }

            foreach (SkillMasterySpell spell in EnumerateSpells(attacker))
            {
                spell.OnGotParried(defender);
            }
        }
		
		public static bool HasMastery(Mobile mobile, SkillName name)
		{
            Skill sk = mobile.Skills[name];

            return sk.IsMastery && sk.KnownMasteries != 0;
		}

        public static bool SetActiveMastery(Mobile mobile, SkillName name)
        {
            return mobile.Skills[name].SetCurrent();
        }
	
		protected virtual void BeginTimer()
		{
			if(Timer != null)
			{
				Timer.Stop();
				Timer = null;
			}
				
			Timer = new UpkeepTimer(this);
            Timer.Start();

            if(Expires < DateTime.UtcNow)
                Expires = DateTime.UtcNow + ExpirationPeriod;

			AddToTable(Caster, this);
			AddStatMods();

            Caster.RevealingAction();

            Caster.Delta(MobileDelta.WeaponDamage);

            if(Target != null)
                Target.Delta(MobileDelta.WeaponDamage);

            if (PartyList != null)
            {
                foreach (Mobile m in PartyList)
                    m.Delta(MobileDelta.WeaponDamage);
            }
		}

        public int GetWeaponSkill()
        {
            double swrd = Caster.Skills[SkillName.Swords].Value;
            double fenc = Caster.Skills[SkillName.Fencing].Value;
            double mcng = Caster.Skills[SkillName.Macing].Value;
            double arch = Caster.Skills[SkillName.Archery].Value;
            double thro = Caster.Skills[SkillName.Throwing].Value;
            double wres = Caster.Skills[SkillName.Wrestling].Value;

            double val = swrd;

            if (fenc > val)
                val = fenc;

            if (mcng > val)
                val = mcng;

            if (arch > val)
                val = arch;

            if (thro > val)
                val = thro;

            if (wres > val)
                val = wres;

            return (int)val;
        }

        public static void OnWeaponRemoved(Mobile from, BaseWeapon weapon)
        {
            foreach (SkillMasterySpell spell in EnumerateSpells(from))
            {
                spell.OnWeaponRemoved(weapon);
            }
        }

        public static bool CancelWeaponAbility(Mobile attacker)
        {
            foreach (SkillMasterySpell spell in EnumerateSpells(attacker))
            {
                if (spell.CancelsWeaponAbility)
                    return true;
            }

            return false;
        }

        public static bool CancelSpecialMove(Mobile attacker)
        {
            foreach (SkillMasterySpell spell in EnumerateSpells(attacker))
            {
                if (spell.CancelsSpecialMove)
                    return true;
            }

            return false;
        }

        private static Dictionary<SkillMasterySpell, DateTime> _Cooldown;

        protected void AddToCooldown(TimeSpan ts)
        {
            if (_Cooldown == null)
                _Cooldown = new Dictionary<SkillMasterySpell, DateTime>();

            _Cooldown[this] = DateTime.UtcNow + ts;

            Server.Timer.DelayCall(ts, () => RemoveFromCooldown(this.GetType(), Caster));
        }

        public static bool IsInCooldown(Mobile m, Type type, bool message = true)
        {
            if (_Cooldown == null)
                return false;

            CheckCooldown();
            bool iscooling = false;

            foreach (KeyValuePair<SkillMasterySpell, DateTime> kvp in _Cooldown)
            {
                SkillMasterySpell spell = kvp.Key;
                DateTime dt = kvp.Value;

                if (!iscooling && spell.GetType() == type && spell.Caster == m)
                {
                    if (message)
                    {
                        double left = (dt - DateTime.UtcNow).TotalMinutes;

                        if (left > 1)
                            m.SendLocalizedMessage(1155787, ((int)left).ToString()); // You must wait ~1_minutes~ minutes before you can use this ability.
                        else
                        {
                            left = (_Cooldown[spell] - DateTime.UtcNow).TotalSeconds;
                            m.SendLocalizedMessage(1079335, left.ToString("F", System.Globalization.CultureInfo.InvariantCulture)); // You must wait ~1_seconds~ seconds before you can use this ability again.
                        }
                    }

                    iscooling = true;
                }
            }

            return iscooling;
        }

        public static void RemoveFromCooldown(Type type, Mobile m)
        {
            SkillMasterySpell spell = null;

            foreach (KeyValuePair<SkillMasterySpell, DateTime> kvp in _Cooldown)
            {
                if (kvp.Key.GetType() == type && kvp.Key.Caster == m)
                    spell = kvp.Key;
            }

            if (spell != null)
                _Cooldown.Remove(spell);

            CheckCooldown();
        }

        public static void CheckCooldown()
        {
            List<SkillMasterySpell> spells = new List<SkillMasterySpell>();

            foreach (KeyValuePair<SkillMasterySpell, DateTime> kvp in _Cooldown)
            {
                if (kvp.Value < DateTime.UtcNow)
                    spells.Add(kvp.Key);
            }

            spells.ForEach(sp => _Cooldown.Remove(sp));

            if (_Cooldown != null && _Cooldown.Count == 0)
                _Cooldown = null;
        }

        public static int GetAttributeBonus(Mobile m, AosAttribute attr)
        {
            int value = 0;
            SkillMasterySpell spell = null;

            switch (attr)
            {
                case AosAttribute.AttackChance:
                    spell = SkillMasterySpell.GetSpellForParty(m, typeof(InspireSpell));
                    if (spell != null)
                        value += spell.PropertyBonus();

                    spell = SkillMasterySpell.GetSpellForParty(m, typeof(TribulationSpell));
                    if (spell != null)
                        value += spell.PropertyBonus();

                    //value += FocusedEyeSpell.HitChanceBonus(m);
                    //value += PlayingTheOddsSpell.HitChanceBonus(m);
                    //value += CalledShotSpell.GetHitChanceBonus(m);
                    //value += CombatTrainingSpell.GetHitChanceBonus(m);
                    break;
                case AosAttribute.DefendChance:
                    spell = SkillMasterySpell.GetSpellForParty(m, typeof(PerseveranceSpell));

                    if (spell != null)
                        value += spell.PropertyBonus();

                    //if (Server.Spells.SkillMasteries.WhiteTigerFormSpell.IsActive(m))
                    //   value += 20;
                    break;
                case AosAttribute.RegenHits:
                    spell = SkillMasterySpell.GetSpellForParty(m, typeof(ResilienceSpell));

                    if (spell != null)
                        value += spell.PropertyBonus();
                    break;
                case AosAttribute.RegenStam:
                    spell = SkillMasterySpell.GetSpellForParty(m, typeof(ResilienceSpell));

                    if (spell != null)
                        value += spell.PropertyBonus();
                    break;
                case AosAttribute.RegenMana:
                    spell = SkillMasterySpell.GetSpellForParty(m, typeof(ResilienceSpell));

                    if (spell != null)
                        value += spell.PropertyBonus();
                    break;
                case AosAttribute.BonusHits:
                    spell = SkillMasterySpell.GetSpellForParty(m, typeof(InvigorateSpell));

                    if (spell != null)
                        value += spell.StatBonus();
                    break;
                case AosAttribute.WeaponDamage:
                    spell = SkillMasterySpell.GetSpellForParty(m, typeof(InspireSpell));

                    if (spell != null)
                        value += spell.DamageBonus();
                    break;
                case AosAttribute.SpellDamage:
                    spell = SkillMasterySpell.GetSpellForParty(m, typeof(InspireSpell));

                    if (spell != null)
                        value += spell.PropertyBonus();
                    break;
                case AosAttribute.WeaponSpeed:
                    //value += RampageSpell.GetBonus(m, RampageSpell.BonusType.SwingSpeed); // Put in before cap
                    //value += PlayingTheOddsSpell.SwingSpeedBonus(m);
                    //value -= StaggerSpell.GetStagger(m);
                    break;
            }

            return value;
        }

        public static int GetAttributeBonus(Mobile m, SAAbsorptionAttribute attr)
        {
            int value = 0;

            switch (attr)
            {
                case SAAbsorptionAttribute.CastingFocus:
                    SkillMasterySpell spell = SkillMasterySpell.GetSpellForParty(m, typeof(PerseveranceSpell));

                    if (spell != null)
                        value += spell.PropertyBonus2();
                    break;
            }

            return value;
        }

        protected virtual void OnTarget(object o)
        {
        }

        public class MasteryTarget : Target
        {
            public SkillMasterySpell Owner { get; set; }
            public bool AutoFinishSequence { get; set; }

            public MasteryTarget(SkillMasterySpell spell, int range = 10, bool allowGround = false, TargetFlags flags = TargetFlags.None, bool autoEnd = true)
                : base(range, allowGround, flags)
            {
                Owner = spell;
                AutoFinishSequence = autoEnd;
            }

            protected override void OnTarget(Mobile from, object o)
            {
                if (o == null)
                    return;

                if (!from.CanSee(o))
                    from.SendLocalizedMessage(500237); // Target can not be seen.
                else
                {
                    SpellHelper.Turn(from, o);
                    Owner.OnTarget(o);
                }
            }

            protected override void OnTargetFinish(Mobile from)
            {
                if(AutoFinishSequence)
                    Owner.FinishSequence();
            }
        }

		public class UpkeepTimer : Timer
		{
			private SkillMasterySpell m_Spell;
				
			public SkillMasterySpell Spell { get { return m_Spell; } }
				
			public UpkeepTimer(SkillMasterySpell spell) : base(TimeSpan.FromSeconds(spell.TickTime), TimeSpan.FromSeconds(spell.TickTime))
			{
				m_Spell = spell;
			}
				
			protected override void OnTick()
			{
				if(m_Spell != null)
					m_Spell.OnTick();
			}
		}

        [Usage("LearnAllMasteries")]
        [Description("Select a target to learn all skill masteries.")]
        public static void LearnAllSpells(CommandEventArgs e)
        {
            e.Mobile.SendMessage("Target the mobile who will learn all skill masteries.");
            e.Mobile.BeginTarget(-1, false, TargetFlags.None, (mobile, targeted) =>
                {
                    if (targeted is Mobile)
                    {
                        Mobile m = targeted as Mobile;
                        int count = 0;

                        foreach (Skill sk in m.Skills)
                        {
                            if (sk.IsMastery)
                            {
                                count++;
                                sk.KnownMasteries = 3;
                            }
                        }

                        e.Mobile.SendMessage("They have learned {0} masteries!", count.ToString());
                    }
                });
        }
	}
}