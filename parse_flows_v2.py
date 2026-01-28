#!/usr/bin/env python3
"""
Parser för A320 Cockpit Flows PDF - Förbättrad version.
"""

import json
import re

# Manuellt definierade flows för bättre kontroll
flows_data = [
    {
        "name": "COCKPIT PREPARATION FLOWS",
        "items": [
            ("PFD LT", "BRT"),
            ("ND LT", "BRT"),
            ("ECAM UPPER DISPLAY", "BRT"),
            ("ECAM LOWER DISPLAY", "BRT"),
            ("FLOOD LT", "AS RQRD"),
            ("INTEG LT", "AS RQRD"),
            # OVERHEAD PANEL
            ("BATTERY", "ON - CHECK VOLTAGE"),
            ("EXTERNAL POWER", "ON"),
            ("IRS/ADIRS", "ON"),
            ("ELEC HYDR PUMP", "ON"),
            ("FUEL PUMPS", "ON"),
            ("ENG GEN", "ON/FAULT"),
            ("PACK 1+2", "ON"),
            ("ENG BLEED 1+2", "ON"),
            ("HOT AIR", "ON"),
            ("ENG ANTI ICE/PROBE", "OFF"),
            ("EMERGENCY LIGHTS", "ARMED"),
            ("CABIN SIGNS", "ON"),
            ("NAV & LOGO LIGHTS", "ON"),
            ("EFC 1+2", "ON"),
            ("GPWS", "ON"),
            # PEDESTAL
            ("PARK BRAKE", "SET"),
            ("FLAPS", "VERIFY 0"),
            ("SPEEDBRAKE", "RETRACTED/DISARMED"),
            ("ENG MASTER 1+2", "OFF"),
            ("ENG MODE SEL", "NORM"),
            ("THRUST LEVERS", "IDLE"),
            ("TRANSPONDER", "STANDBY"),
            ("RADIO CONTROL PANEL", "ON"),
            ("FREQUENCIES", "SET"),
            # ECAM & MAIN PANEL
            ("ECAM RECALL BUTTON", "SELECT"),
            ("LANDING GEAR", "VERIFY DOWN"),
            ("MCDU2 DOORS", "VERIFY OPEN"),
            ("ANTISKID/NWS", "ON"),
            ("FD", "ON"),
        ]
    },
    {
        "name": "FMGS SETUP",
        "items": [
            ("ATIS/ATC CLEARANCE", "OBTAIN"),
            ("TRANSPONDER CODE", "SET"),
            ("MCDU", "SET"),
            ("QNH MODE", "VERIFY"),
            ("QNH", "SET"),
            ("ND MODE/RANGE", "SET"),
            ("VOR/ADF", "SELECT"),
            ("SPEED MANAGED/SELECTED", "AS RQRD"),
            ("HDG MANAGED/SELECTED", "AS RQRD"),
            ("INITIAL ALTITUDE SET/MANAGED/SELECTED", "AS RQRD"),
            ("IRS", "CONFIRM ALIGNED"),
            ("FD", "CYCLE OFF THEN ON"),
            ("LS", "OFF"),
            ("FCU", "VERIFY CORRECT & COMPLETE"),
        ]
    },
    {
        "name": "BEFORE START FLOWS",
        "items": [
            ("FUELING", "VERIFY DISCONNECTED, KG, BALANCED, & SUFFICIENT"),
            ("CHOCKS", "REMOVED"),
            ("TRAFFIC CONES", "REMOVED"),
            # OVERHEAD PANEL
            ("APU", "START"),
            ("APU BLEED", "ON"),
            ("EXT POWER", "OFF"),
            ("DOORS", "CLOSED"),
            ("BEACON", "ON"),
        ]
    },
    {
        "name": "PUSHBACK & ENGINE START FLOWS",
        "items": [
            ("PUSH & START CLEARANCE", "OBTAIN"),
            ("GROUND COMMUNICATION", "INITIATE/FOLLOW COMMANDS"),
            ("RIGHT SIDE", "VERIFY CLEAR"),
            # PEDESTAL
            ("ENG MODE SELECTOR", "IGN/START"),
            ("ENG MASTER 2", "ON"),
            # ECAM
            ("ECAM", "MONITOR"),
            ("ENGINE", "CONFIRM STABILIZED"),
            ("STEP 3, 5-7", "REPEAT FOR ENG 1"),
        ]
    },
    {
        "name": "AFTER START FLOWS",
        "items": [
            # OVERHEAD PANEL
            ("ENG & WING ANTI ICE", "AS RQRD"),
            ("APU BLEED", "OFF"),
            ("APU MASTER", "OFF"),
            # PEDESTAL
            ("PITCH TRIM", "SET"),
            ("ENG MODE SELECTOR", "AS RQRD"),
            ("SPOILERS", "ARMED"),
            ("FLAPS", "SET"),
            ("RUD TRIM", "0"),
            # ECAM
            ("FLIGHT CONTROLS", "CHECK"),
            ("ECAM DOOR PAGE", "CHECKED"),
            ("ECAM STATUS", "CHECKED"),
            # OTHER
            ("HAND SIGNALS", "RECEIVED"),
        ]
    },
    {
        "name": "TAXI FLOWS",
        "items": [
            ("TAXI CLEARANCE", "OBTAIN"),
            ("PARK BRAKE", "RELEASED"),
            ("AUTO BRAKE", "MAX"),
            ("TO CONFIG", "PRESS"),
            ("TAXI LIGHT", "ON"),
        ]
    },
    {
        "name": "BEFORE TAKE OFF FLOWS",
        "items": [
            # PEDESTAL
            ("TCAS", "TA/RA - TILT ABOVE"),
            ("ENG MODE SELECTOR", "AS RQRD"),
            # ECAM
            ("BRAKE TEMP", "CHECK GREATER THAN 150"),
            # MAIN PANEL
            ("BRAKE FANS", "OFF"),
            ("SLIDING TABLE", "STOWED"),
            # ATC
            ("TAKE OFF/LINE UP CLEARANCE", "OBTAIN"),
            # OVERHEAD PANEL
            ("EXT LIGHTS", "ON"),
        ]
    },
    {
        "name": "AFTER TAKE OFF FLOWS",
        "note": "At acceleration height",
        "items": [
            ("SPOILER", "DISARM"),
            ("ENGINE MODE SELECTOR", "AS RQRD"),
            ("TAXI LIGHT", "OFF"),
            ("ENG & WING ANTI ICE", "AS RQRD"),
            ("PACK 1+2", "ON"),
        ]
    },
    {
        "name": "10000 FT CLIMB FLOWS",
        "items": [
            ("EXT LIGHTS", "OFF"),
            ("SEAT BELT SIGN", "AS RQRD"),
            ("PRESSURIZATION", "CHECKED"),
        ]
    },
    {
        "name": "PREDESCENT FLOWS",
        "items": [
            ("ATIS", "OBTAIN"),
            ("MCDU", "FPLN, PERF, NAV/RAD SET"),
            ("NAV ACCURACY", "CHECK"),
            ("AUTO BRAKE", "SET"),
            ("SEAT BELT SIGN", "ON"),
            ("ANTI ICE", "AS RQRD"),
        ]
    },
    {
        "name": "10000 FT DESCENT FLOWS",
        "items": [
            ("SLIDING TABLES", "STOWED"),
            ("EXT LIGHTS", "ON"),
            ("SEAT BELT SIGN", "VERIFY ON"),
            ("LS", "PUSH"),
            ("PRESSURIZATION", "CHECKED"),
            ("ECAM STATUS", "CHECK"),
        ]
    },
    {
        "name": "LANDING FLOWS",
        "note": "When fully configured and established on approach",
        "items": [
            ("TAXI LIGHT", "SET T/O"),
            ("MISSED APPROACH ALTITUDE", "SET"),
            ("SPOILERS", "ARMED"),
            ("LANDING MEMO", "NO BLUE"),
        ]
    },
    {
        "name": "AFTER LANDING FLOWS",
        "items": [
            ("SPOILERS", "RETRACT"),
            ("ENG MODE SELECTOR", "NORM"),
            ("FLAPS", "RETRACT"),
            ("TRANSPONDER", "STBY"),
            ("BRAKE TEMP", "CHECK GREATER THAN 300"),
            ("EXT LIGHTS", "OFF"),
            ("APU", "START"),
            ("ENGINE & WING ANTI ICE", "OFF"),
        ]
    },
    {
        "name": "PARKING FLOWS",
        "items": [
            ("PARK BRAKE", "SET"),
            ("ENG MASTER 1+2", "OFF"),
            ("SEAT BELT SIGN", "OFF"),
            ("BEACON", "OFF"),
            ("EXT POWER", "ON"),
            ("FUEL PUMPS", "OFF"),
            ("APU", "AS RQRD"),
        ]
    },
    {
        "name": "SECURING FLOWS",
        "items": [
            ("PFD LT", "OFF"),
            ("ND LT", "OFF"),
            ("ECAM UPPER LT", "OFF"),
            ("ECAM LOWER LT", "OFF"),
            ("ADIRS", "OFF"),
            ("EXT POWER", "OFF"),
            ("GEN", "ON"),
            ("APU", "SHUTDOWN"),
            ("CABIN SIGNS & EMERGENCY LIGHTS", "OFF"),
            ("BAT 1+2", "OFF"),
        ]
    },
]

def expand_abbreviations(text):
    """Expandera förkortningar för naturligare TTS."""
    expansions = {
        # Ljus/Display
        r'\bLT\b': 'Light',
        r'\bBRT\b': 'Bright',
        r'\bINTEG\b': 'Integral',
        
        # Allmänna förkortningar
        r'\bRQRD\b': 'Required',
        r'\bAS RQRD\b': 'As Required',
        r'\bEXT\b': 'External',
        r'\bSEL\b': 'Selector',
        r'\bNORM\b': 'Normal',
        r'\bSTBY\b': 'Standby',
        r'\bCONFIG\b': 'Configuration',
        r'\bTEMP\b': 'Temperature',
        r'\bALT\b': 'Altitude',
        
        # Motor/System
        r'\bENG\b': 'Engine',
        r'\bGEN\b': 'Generator',
        r'\bHYDR\b': 'Hydraulic',
        r'\bELEC\b': 'Electric',
        r'\bIGN\b': 'Ignition',
        r'\bRUD\b': 'Rudder',
        
        # Navigation
        r'\bNAV\b': 'Navigation',
        r'\bHDG\b': 'Heading',
        r'\bFPLN\b': 'Flight Plan',
        r'\bPERF\b': 'Performance',
        r'\bNAV/RAD\b': 'Nav Radio',
        
        # Enheter
        r'\bFT\b': 'Feet',
        r'\bKG\b': 'Kilograms',
        
        # Akronymer som ska uttalas bokstav för bokstav
        r'\bAPU\b': 'A P U',
        r'\bIRS\b': 'I R S',
        r'\bADIRS\b': 'A D I R S',
        r'\bQNH\b': 'Q N H',
        r'\bND\b': 'Navigation Display',
        r'\bPFD\b': 'P F D',
        r'\bECAM\b': 'E-CAM',
        r'\bMCDU\b': 'M C D U',
        r'\bMCDU2\b': 'M C D U 2',
        r'\bFCU\b': 'F C U',
        r'\bLS\b': 'L S',
        r'\bEFC\b': 'E F C',
        r'\bGPWS\b': 'G P W S',
        r'\bVOR\b': 'V O R',
        r'\bADF\b': 'A D F',
        r'\bATC\b': 'A T C',
        r'\bATIS\b': 'A T I S',
        r'\bTCAS\b': 'T-CAS',
        r'\bFMGS\b': 'F M G S',
        r'\bNWS\b': 'Nose Wheel Steering',
        r'\bBAT\b': 'Battery',
        r'\bFD\b': 'Flight Director',
        
        # Specifika fraser
        r'\bTA/RA\b': 'T A, R A',
        r'\bT/O\b': 'Takeoff',
        r'\bTO CONFIG\b': 'Takeoff Config',
        r'\b1\+2\b': '1 and 2',
        r'\b>150\b': 'greater than 150',
        r'\b>300\b': 'greater than 300',
    }
    
    result = text
    for pattern, replacement in expansions.items():
        result = re.sub(pattern, replacement, result, flags=re.IGNORECASE)
    
    return result

def generate_safe_filename(flow_name, index, item_name):
    """Generera ett säkert filnamn."""
    flow_part = re.sub(r'[^a-z0-9]', '_', flow_name.lower())
    flow_part = re.sub(r'_+', '_', flow_part).strip('_')
    
    item_part = re.sub(r'[^a-z0-9]', '_', item_name.lower())
    item_part = re.sub(r'_+', '_', item_part).strip('_')[:30]
    
    return f"{flow_part}_{index:02d}_{item_part}.wav"

def build_flows():
    """Bygg flows-strukturen."""
    flows = []
    audio_files = []
    
    for flow_data in flows_data:
        flow = {
            'name': flow_data['name'],
            'trigger_phrase': flow_data['name'].lower(),
            'items': []
        }
        
        if 'note' in flow_data:
            flow['note'] = flow_data['note']
        
        # Ljudfil för flow-start (bara namnet)
        flow_name_safe = re.sub(r'[^a-z0-9]', '_', flow_data['name'].lower())
        flow_name_safe = re.sub(r'_+', '_', flow_name_safe).strip('_')
        
        flow_name_spoken = expand_abbreviations(flow_data['name'])
        
        audio_files.append({
            'id': f"{flow_name_safe}_start",
            'text': flow_name_spoken,
            'filename': f"{flow_name_safe}_start.wav",
            'flow': flow_data['name'],
            'type': 'flow_start'
        })
        
        # Ljudfil för flow-complete
        audio_files.append({
            'id': f"{flow_name_safe}_complete",
            'text': f"{flow_name_spoken} complete",
            'filename': f"{flow_name_safe}_complete.wav",
            'flow': flow_data['name'],
            'type': 'flow_complete'
        })
        
        for i, (item, response) in enumerate(flow_data['items']):
            flow['items'].append({
                'item': item,
                'response': response
            })
            
            # Skapa audio entry
            audio_text = f"{item}: {response}"
            audio_text_expanded = expand_abbreviations(audio_text)
            
            filename = generate_safe_filename(flow_data['name'], i, item)
            
            audio_files.append({
                'id': f"{flow_data['name'].lower().replace(' ', '_')}_{i}",
                'text': audio_text_expanded,
                'filename': filename,
                'flow': flow_data['name'],
                'item': item,
                'response': response
            })
        
        flows.append(flow)
    
    return flows, audio_files

# Bygg data
flows, audio_files = build_flows()

# Sammanfattning
print("=" * 60)
print("A320 COCKPIT FLOWS - PARSED DATA")
print("=" * 60)

total_items = sum(len(f['items']) for f in flows)
print(f"\nTotal flows: {len(flows)}")
print(f"Total items: {total_items}")
print(f"Audio files needed: {len(audio_files)}")

print("\n" + "-" * 60)
print("FLOWS:")
print("-" * 60)
for flow in flows:
    note = f" ({flow['note']})" if 'note' in flow else ""
    print(f"  {flow['name']}: {len(flow['items'])} items{note}")

print("\n" + "-" * 60)
print("SAMPLE AUDIO TEXTS:")
print("-" * 60)
for af in audio_files[:15]:
    print(f"  {af['text']}")

# Spara JSON-filer
with open('/home/claude/flows.json', 'w', encoding='utf-8') as f:
    json.dump(flows, f, indent=2, ensure_ascii=False)

with open('/home/claude/audio_files.json', 'w', encoding='utf-8') as f:
    json.dump(audio_files, f, indent=2, ensure_ascii=False)

print("\n" + "=" * 60)
print("Saved: flows.json, audio_files.json")
print("=" * 60)
