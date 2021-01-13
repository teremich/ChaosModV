#include <stdafx.h>

static void OnStop()
{
	Memory::SetHealthArmorBarHidden(false);

	SET_PLAYER_HEALTH_RECHARGE_MULTIPLIER(PLAYER_ID(), 1.f);

	for (Ped ped : GetAllPeds())
	{
		if (!IS_PED_DEAD_OR_DYING(ped, true))
		{
			SET_ENTITY_HEALTH(ped, GET_PED_MAX_HEALTH(ped), 0);
		}
	}
}

static void OnTick()
{
	Memory::SetHealthArmorBarHidden(true);

	SET_PLAYER_HEALTH_RECHARGE_MULTIPLIER(PLAYER_ID(), .0f);

	for (Ped ped : GetAllPeds())
	{
		if (!IS_PED_DEAD_OR_DYING(ped, true) && GET_ENTITY_HEALTH(ped) > 101)
		{
			SET_ENTITY_HEALTH(ped, 101, 0);
			SET_PED_ARMOUR(ped, 0);
		}
	}
}

static RegisterEffect registerEffect(EFFECT_PEDS_OHKO, nullptr, OnStop, OnTick);