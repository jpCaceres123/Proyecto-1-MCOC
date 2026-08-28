"""Simple geometry and global axes plot."""
from pathlib import Path
import json
import matplotlib.pyplot as plt
from mpl_toolkits.mplot3d import Axes3D  # noqa: F401

out = Path(__file__).parent / 'results'
nodes = {1:(0,0,0), 2:(6,0,0), 3:(6,5,0), 4:(0,5,0), 5:(0,0,3), 6:(6,0,3), 7:(6,5,3), 8:(0,5,3)}
members = [(1,5),(2,6),(3,7),(4,8),(5,6),(8,7),(5,8),(6,7)]
fig = plt.figure(figsize=(11, 8)); ax = fig.add_subplot(111, projection='3d')
for i,j in members:
    x,y,z = zip(nodes[i], nodes[j]); ax.plot(x,y,z,'k-',lw=2)
for n,(x,y,z) in nodes.items(): ax.scatter(x,y,z,c='tab:blue'); ax.text(x,y,z,f' {n}')
ax.quiver(0,0,0,1,0,0,color='r',length=1); ax.quiver(0,0,0,0,1,0,color='g',length=1); ax.quiver(0,0,0,0,0,1,color='b',length=1)
ax.set(xlabel='X [m]', ylabel='Y [m]', zlabel='Z [m]', title='Marco 3D: geometría y ejes globales')
fig.tight_layout(); fig.savefig(out / 'geometry.png', dpi=180); print(out / 'geometry.png')

# Additional view of the nodes generated on the upper beam perimeter.
model = json.loads((out / 'model.json').read_text(encoding='utf-8'))
all_nodes = {n['nodeTag']: (n['x_m'], n['y_m'], n['z_m']) for n in model['nodes']}
fig = plt.figure(figsize=(11, 8)); ax = fig.add_subplot(111, projection='3d')
for element in model['elements']:
    x, y, z = zip(all_nodes[element['nodeI']], all_nodes[element['nodeJ']])
    ax.plot(x, y, z, color='0.75', lw=1.0)
base_nodes = [n for n in all_nodes if n <= 4]
generated_nodes = [n for n in all_nodes if n > 8]
ax.scatter(*zip(*(all_nodes[n] for n in base_nodes)), c='tab:blue', s=45, label='Apoyos')
ax.scatter(*zip(*(all_nodes[n] for n in generated_nodes)), c='tab:orange', s=28,
           label='Nodos generados en líneas 51-63')
for n in all_nodes:
    x, y, z = all_nodes[n]
    if n in base_nodes or n in generated_nodes:
        ax.text(x, y, z, f' {n}', fontsize=7,
                color='tab:blue' if n in base_nodes else 'tab:orange')
ax.set(xlabel='X [m]', ylabel='Y [m]', zlabel='Z [m]',
       title='Nodos de discretización de las vigas superiores')
ax.legend()
fig.tight_layout(); fig.savefig(out / 'discretization.png', dpi=180); print(out / 'discretization.png')
