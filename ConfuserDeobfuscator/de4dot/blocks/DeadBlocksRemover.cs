﻿/*
    Copyright (C) 2011-2013 de4dot@gmail.com

    This file is part of de4dot.

    de4dot is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    de4dot is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with de4dot.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;

namespace de4dot.blocks {
	class DeadBlocksRemover {
		MethodBlocks methodBlocks;
		Dictionary<BaseBlock, bool> checkedBaseBlocks = new Dictionary<BaseBlock, bool>();
		Dictionary<ScopeBlock, bool> checkedScopeBlocks = new Dictionary<ScopeBlock, bool>();
		Stack<BaseBlock> baseBlocksToCheck = new Stack<BaseBlock>();
		Stack<ScopeBlock> scopeBlocksToCheck = new Stack<ScopeBlock>();

		public DeadBlocksRemover(MethodBlocks methodBlocks) {
			this.methodBlocks = methodBlocks;
		}

		public int remove() {
			addScopeBlock(methodBlocks);
			processAll();
			return removeDeadBlocks();
		}

		class ScopeBlockInfo {
			public ScopeBlock scopeBlock;
			public IList<BaseBlock> deadBlocks = new List<BaseBlock>();
			public ScopeBlockInfo(ScopeBlock scopeBlock) {
				this.scopeBlock = scopeBlock;
			}
		}

		int removeDeadBlocks() {
			int numDeadBlocks = 0;

			var infos = new Dictionary<ScopeBlock, ScopeBlockInfo>();
			var deadBlocksDict = new Dictionary<BaseBlock, bool>();
			foreach (var baseBlock in findDeadBlocks()) {
				deadBlocksDict[baseBlock] = true;
				ScopeBlock parent = baseBlock.Parent;
				ScopeBlockInfo info;
				if (!infos.TryGetValue(parent, out info))
					infos[parent] = info = new ScopeBlockInfo(parent);
				info.deadBlocks.Add(baseBlock);
				numDeadBlocks++;
			}

			foreach (var info in infos.Values)
				info.scopeBlock.removeAllDeadBlocks(info.deadBlocks, deadBlocksDict);

			return numDeadBlocks;
		}

		IList<BaseBlock> findDeadBlocks() {
			var deadBlocks = new List<BaseBlock>();

			foreach (var bb in methodBlocks.getAllBaseBlocks()) {
				if (!checkedBaseBlocks.ContainsKey(bb))
					deadBlocks.Add(bb);
			}

			return deadBlocks;
		}

		void addScopeBlock(ScopeBlock scopeBlock) {
			scopeBlocksToCheck.Push(scopeBlock);
		}

		void processAll() {
			bool didSomething;
			do {
				didSomething = false;
				while (baseBlocksToCheck.Count > 0) {
					processBaseBlock(baseBlocksToCheck.Pop());
					didSomething = true;
				}
				while (scopeBlocksToCheck.Count > 0) {
					processScopeBlock(scopeBlocksToCheck.Pop());
					didSomething = true;
				}
			} while (didSomething);
		}

		void processBaseBlock(BaseBlock baseBlock) {
			if (baseBlock == null || checkedBaseBlocks.ContainsKey(baseBlock))
				return;
			checkedBaseBlocks[baseBlock] = true;

			if (baseBlock is Block) {
				var block = (Block)baseBlock;
				foreach (var block2 in block.getTargets())
					addBaseBlock(block2);
			}
			else if (baseBlock is ScopeBlock) {
				var scopeBlock = (ScopeBlock)baseBlock;
				addScopeBlock(scopeBlock);
				if (scopeBlock.BaseBlocks != null && scopeBlock.BaseBlocks.Count > 0)
					addBaseBlock(scopeBlock.BaseBlocks[0]);
			}
			else
				throw new ApplicationException(string.Format("Unknown BaseBlock type {0}", baseBlock.GetType()));
		}

		// Add a block to be processed later, including all its enclosing ScopeBlocks.
		void addBaseBlock(BaseBlock baseBlock) {
			for (BaseBlock bb = baseBlock; bb != null; bb = bb.Parent)
				baseBlocksToCheck.Push(bb);
		}

		void processScopeBlock(ScopeBlock scopeBlock) {
			if (scopeBlock == null || checkedScopeBlocks.ContainsKey(scopeBlock))
				return;
			checkedScopeBlocks[scopeBlock] = true;
			addBaseBlock(scopeBlock);

			if (scopeBlock is TryBlock) {
				var tryBlock = (TryBlock)scopeBlock;
				foreach (var handler in tryBlock.TryHandlerBlocks)
					addScopeBlock(handler);
			}
			else if (scopeBlock is TryHandlerBlock) {
				var tryHandlerBlock = (TryHandlerBlock)scopeBlock;
				addScopeBlock(tryHandlerBlock.FilterHandlerBlock);
				addScopeBlock(tryHandlerBlock.HandlerBlock);
			}
		}
	}
}
