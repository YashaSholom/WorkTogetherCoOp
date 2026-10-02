"""Matching astronaut view-model arms. Uses the existing first-person export frame."""
import sys, os
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), 'BeanCrew'))
from common import *
reset()
ivory=mat('Planet Ivory',(224,214,191));rubber=mat('Planet Rubber',(19,17,22))
objects=[]
for name,color in [('Bolt',(26,94,224)),('Gus',(255,173,3)),('Pip',(217,23,15)),('Dot',(89,186,15))]:
    suit=mat('Planet Suit '+name,color)
    for side in ['L','R']:
        s=1 if side=='R' else -1
        parts=[tube('Sleeve',[(0,-.1,0),(0,.20,0),(0,.40,0)],[.085,.08,.074],suit,seg=24,steps=4),
               tube('White cuff',[(0,.40,0),(0,.445,0)],[.081,.079],ivory,seg=24,steps=2),
               sphere('Black glove',(0,.55,0),(.08,.09,.07),rubber,32,20),
               sphere('Knuckles',(0,.59,.012),(.072,.045,.057),rubber,24,16),
               sphere('Thumb',(-s*.06,.54,.025),(.035,.052,.034),rubber,24,16)]
        objects.append(join('FP_'+name+'_'+side,parts))
export_fbx('D:/WorkTogetherCoOp/CoOpTest/Assets/CoopPrototype/Art/Planet/PlanetArms.fbx',objects)
print('PLANET_ARMS_EXPORTED')
