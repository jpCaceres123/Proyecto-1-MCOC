"""Simple geometry and global axes plot."""
from pathlib import Path
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
