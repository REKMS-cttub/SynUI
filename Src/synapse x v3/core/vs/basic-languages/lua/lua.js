/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
define(["require", "exports"], function (require, exports) {
    'use strict';
    Object.defineProperty(exports, "__esModule", { value: true });
    exports.conf = {
        comments: {
            lineComment: '--',
            blockComment: ['--[[', ']]']
        },
        brackets: [
            ['{', '}'],
            ['[', ']'],
            ['(', ')'],
            ['do', 'end'],
            ['then', 'end'],
            ['repeat', 'until']
        ],
        autoClosingPairs: [
            { open: '{', close: '}' },
            { open: '[', close: ']' },
            { open: '(', close: ')' },
            { open: '"', close: '"' },
            { open: '\'', close: '\'' },
        ],
        surroundingPairs: [
            { open: '{', close: '}' },
            { open: '[', close: ']' },
            { open: '(', close: ')' },
            { open: '"', close: '"' },
            { open: '\'', close: '\'' },
        ],
        folding: {
            markers: {
                start: new RegExp("^\\s*//\\s*(?:(?:#?region\\b)|(?:<editor-fold\\b))"),
                end: new RegExp("^\\s*//\\s*(?:(?:#?endregion\\b)|(?:</editor-fold>))")
            }
        }
    };
    exports.language = {
        defaultToken: '',
        tokenPostfix: '.lua',
        keywords: 'and break do else elseif end false for function if in local nil not or repeat return then true until while next continue _G getgenv getrenv'.split(' '),
        brackets: [
            { token: 'delimiter.bracket', open: '{', close: '}' },
            { token: 'delimiter.array', open: '[', close: ']' },
            { token: 'delimiter.parenthesis', open: '(', close: ')' }
        ],
        globals: [
            // custom
            'iscclosure', 'islclosure', 'getnamecallmethod', 'getrawmetatable',
            'setrawmetatable', '__namecall', '__index', '__eq', '__call',
            '__newindex', '__tostring', 'hookfunction', 'request',
            'loadstring', 'writefile', 'readfile', 'isreadonly', 'setreadonly',
            'sethiddenproperty', 'getcallingscript', 'pcall',

            // ROBLOX DATATYPES
            'Region3', 'Vector3', 'Vector2', 'UDim2', 'UDim', 'CFrame',
            'Color3', 'Enum', 'Instance', 'DateTime', 'os', 'Vector2int16',
            'Vector3int16', 'Rect', 'Ray', 'BrickColor',

            'workspace', 'spawn', 'delay', 'wait', 'tick', 'getfenv', 'type', 'typeof',
            'KRNL_LOADED', 'coroutine',

            // basic
            'print', 'error', 'warn', 'require', 'game', 'assert',

            // raws
            'rawset', 'rawget', 'rawequal',

            // Drawing
            'Drawing', 'Drawing.new',
            
            // debug
            'getupvalue', 'debug.getupvalue',
            'getconstant', 'debug.getconstant',
            'setstack', 'debug.setstack',
            'getproto', 'debug.getproto',
            'getstack', 'debug.getstack',
            'getfunctionname', 'debug.getfunctionname',
            'profilebegin', 'debug.profilebegin',
            'getprotos', 'debug.getprotos',
            'traceback', 'debug.traceback',
            'getconstants', 'debug.getconstants',
            'getinfo', 'debug.getinfo',
            'setupvalue', 'debug.setupvalue',
            'setconstant', 'debug.setconstant',
            'profileend', 'debug.profileend',
            'getupvalues', 'debug.getupvalues',

            // table
            'table',
            'pack', 'table.pack',
            'move', 'table.move',
            'insert', 'table.insert',
            'getn', 'table.getn',
            'foreachi', 'table.foreachi',
            'maxn', 'table.maxn',
            'foreach', 'table.foreach',
            'concat', 'table.concat',
            'unpack', 'table.unpack',
            'find', 'table.find',
            'create', 'table.create',
            'sort', 'table.sort',
            'remove', 'table.remove',      
            
            // bit
            'bit32',
            'bit32.band',
            'bit32.extract',
            'bit32.bor',
            'bit32.bnot',
            'bit32.arshift',
            'bit32.rshift',
            'bit32.rrotate',
            'bit32.replace',
            'bit32.lshift',
            'bit32.lrotate',
            'bit32.btest',
            'bit32.bxor',

            // math
            'math',
            'math.log',
            'math.ldexp',
            'math.rad',
            'math.cosh',
            'math.random',
            'math.frexp',
            'math.tanh',
            'math.floor',
            'math.max',
            'math.sqrt',
            'math.modf',
            'math.huge',
            'math.pow',
            'math.atan',
            'math.tan',
            'math.cos',
            'math.sign',
            'math.clamp',
            'math.log10',
            'math.noise',
            'math.acos',
            'math.abs',
            'math.pi',
            'math.sinh',
            'math.asin',
            'math.min',
            'math.deg',
            'math.fmod',
            'math.randomseed',
            'math.atan2',
            'math.ceil',
            'math.sin',
            'math.exp',

            // string
            'string',
            'string.sub',
            'string.split',
            'string.upper',
            'string.len',
            'string.find',
            'string.match',
            'string.char',
            'string.rep',
            'string.gmatch',
            'string.reverse',
            'string.byte',
            'string.format',
            'string.gsub',
            'string.lower',

        ],
        operators: [
            '+', '-', '*', '/', '%', '^', '#', '==', '~=', '<=', '>=', '<', '>', '=',
            ';', ':', ',', '.', '..', '...'
        ],
        // we include these common regular expressions
        symbols: /[=><!~?:&|+\-*\/\^%]+/,
        escapes: /\\(?:[abfnrtv\\"']|x[0-9A-Fa-f]{1,4}|u[0-9A-Fa-f]{4}|U[0-9A-Fa-f]{8})/,
        // The main tokenizer for our languages
        tokenizer: {
            root: [
                // identifiers and keywords
                [/[a-zA-Z_]\w*/, {
                        cases: {
                            '@keywords': { token: 'keyword.$0' },
                            '@globals': { token: 'global' },
                            '@default': 'identifier'
                        }
                    }],
                // whitespace
                { include: '@whitespace' },
                // keys
                [/(,)(\s*)([a-zA-Z_]\w*)(\s*)(:)(?!:)/, ['delimiter', '', 'key', '', 'delimiter']],
                [/({)(\s*)([a-zA-Z_]\w*)(\s*)(:)(?!:)/, ['@brackets', '', 'key', '', 'delimiter']],
                // delimiters and operators
                [/[{}()\[\]]/, '@brackets'],
                [/@symbols/, {
                        cases: {
                            '@operators': 'delimiter',
                            '@default': ''
                        }
                    }],
                // numbers
                [/\d*\.\d+([eE][\-+]?\d+)?/, 'number.float'],
                [/0[xX][0-9a-fA-F_]*[0-9a-fA-F]/, 'number.hex'],
                [/\d+?/, 'number'],
                // delimiter: after number because of .\d floats
                [/[;,.]/, 'delimiter'],
                // strings: recover on non-terminated strings
                [/"([^"\\]|\\.)*$/, 'string.invalid'],
                [/'([^'\\]|\\.)*$/, 'string.invalid'],
                [/"/, 'string', '@string."'],
                [/'/, 'string', '@string.\''],
            ],
            whitespace: [
                [/[ \t\r\n]+/, ''],
                [/--\[([=]*)\[/, 'comment', '@comment.$1'],
                [/\[([=]*)\[/, 'comment', '@comment.$1'],
                [/--.*$/, 'comment'],
            ],
            comment: [
                [/[^\]]+/, 'comment'],
                [/\]([=]*)\]/, {
                        cases: {
                            '$1==$S2': { token: 'comment', next: '@pop' },
                            '@default': 'comment'
                        }
                    }],
                [/./, 'comment']
            ],
            string: [
                [/[^\\"']+/, 'string'],
                [/@escapes/, 'string.escape'],
                [/\\./, 'string.escape.invalid'],
                [/["']/, {
                        cases: {
                            '$#==$S2': { token: 'string', next: '@pop' },
                            '@default': 'string'
                        }
                    }]
            ],
        },
    };
});